using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GPUShapeOfModel : MonoBehaviour
{

	const int MaxResolution = 1000;

	[SerializeField]
	private List<Mesh> meshes;

	private float sqrtVertices;

	private bool isTransitioning;

	private float transitionDuration;
	private float vertexCount;

	private static readonly int
		PositionsId = Shader.PropertyToID("positions"),
		TrianglesId = Shader.PropertyToID("triangle_buffer"),
		VerticesId = Shader.PropertyToID("vertex_buffer"),
		ResolutionId = Shader.PropertyToID("resolution"),
		TrianglesCountId = Shader.PropertyToID("triangles_count"),
		StepId = Shader.PropertyToID("_step"),
		ScaleId = Shader.PropertyToID("scale"),
		TransitionProgressId = Shader.PropertyToID("transition_progress"),
		PointsPerTriangleId = Shader.PropertyToID("points_per_triangle"),
		TotalCountId = Shader.PropertyToID("positions_count"),
		CalculationKernelIndex = 0,
		TransformKernelIndex = 1;

	[SerializeField] ComputeShader computeShader;

	[SerializeField] Material material;

	[SerializeField] Mesh mesh;

	[SerializeField, Range(10, MaxResolution)]
	int resolution = 10;

	ComputeBuffer positionsBuffer;
	ComputeBuffer totalArea;
	ComputeBuffer triAngle;
	
	ComputeBuffer vertexBuffer;
	ComputeBuffer trianglesBuffer;
	
	private int totalPointsCount;

	private int currentMeshIndex = -1;
	private int targetMeshIndex = 33;

	private int groupX;
	private int groupY;
	private float step;

	private void OnEnable()
	{
		targetMeshIndex = Random.Range(0, meshes.Count);
		CalculateCurrentMeshPositions(CalculationKernelIndex);
		InitTargetBuffersIndex();
	}

	private void CalculateCurrentMeshPositions(int kernelIndex)
	{
		var currentMesh = meshes[targetMeshIndex];
		positionsBuffer = new ComputeBuffer(resolution * resolution, 3 * sizeof(float));
		vertexBuffer = new ComputeBuffer(currentMesh.vertexCount, 3 * sizeof(float));
		vertexBuffer.SetData(currentMesh.vertices);
		vertexCount = vertexBuffer.count;

		var triangles = currentMesh.triangles;
		trianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
		trianglesBuffer.SetData(triangles);

		computeShader.SetBuffer(kernelIndex, VerticesId, vertexBuffer);
		computeShader.SetBuffer(kernelIndex, TrianglesId, trianglesBuffer);
		computeShader.SetBuffer(kernelIndex, PositionsId, positionsBuffer);
		computeShader.SetInt(ResolutionId, resolution);
		computeShader.SetInt(TrianglesCountId, triangles.Length);

		var size = currentMesh.bounds.size;
		var sizeMagnitude = size.magnitude;
		var magnitude = Mathf.Clamp(sizeMagnitude, 4, 5);
		computeShader.SetFloat(ScaleId,  magnitude / sizeMagnitude);

		var numPerTri = (int)(resolution * resolution / (triangles.Length / 3f));
		groupX = Mathf.CeilToInt(triangles.Length / 3f / 10f);
		groupY = Mathf.CeilToInt(numPerTri / 10f);
		totalPointsCount = numPerTri * (triangles.Length / 3) - 1;
		
		computeShader.SetInt(TotalCountId, totalPointsCount);
		computeShader.SetInt(PointsPerTriangleId, numPerTri);
		computeShader.Dispatch(kernelIndex, groupX, groupY, 1);
			
		step = 2f / Mathf.Lerp(950, 250, resolution / vertexCount);
		material.SetBuffer(PositionsId, positionsBuffer);
		material.SetFloat(StepId, step);
		
	}

	private void InitTargetBuffersIndex()
	{
		if (isTransitioning)
		{
			isTransitioning = false;
			currentMeshIndex = targetMeshIndex;
		}
		else
		{
			do
			{
				targetMeshIndex = Random.Range(0, meshes.Count);
			} while (currentMeshIndex == targetMeshIndex);

			ReleaseBuffers();
			transitionDuration = 0;
			var targetMesh = meshes[targetMeshIndex];
			vertexBuffer = new ComputeBuffer(targetMesh.vertexCount, 3 * sizeof(float));
			vertexBuffer.SetData(targetMesh.vertices);

			var triangles = targetMesh.triangles;
			trianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
			trianglesBuffer.SetData(triangles);

			computeShader.SetBuffer(TransformKernelIndex, VerticesId, vertexBuffer);
			computeShader.SetBuffer(TransformKernelIndex, TrianglesId, trianglesBuffer);
			computeShader.SetInt(TrianglesCountId, trianglesBuffer.count);
			computeShader.SetBuffer(TransformKernelIndex, PositionsId, positionsBuffer);
			
			var size = targetMesh.bounds.size;
			var sizeMagnitude = size.magnitude;
			var magnitude = Mathf.Clamp(sizeMagnitude, 4f, 5f);
			computeShader.SetFloat(ScaleId, magnitude / sizeMagnitude);

			isTransitioning = true;

			var random = Random.value;
			var ease =
				random < 0.25f ? Ease.InCubic :
				random < 0.50f ? Ease.InExpo :
				random < 0.75f ? Ease.InBack : 
				Ease.InSine;
				
			var targetCore = DOTween.To(() => transitionDuration, duration => transitionDuration = duration, 1f, 2f)
				.SetEase(ease)
				.OnUpdate(CalculateTransitioning);
				
			DOTween.Sequence()
				.Append(targetCore)
				.AppendInterval(1.5f)
				.AppendCallback(InitTargetBuffersIndex)
				.AppendInterval(0.5f)
				.OnComplete(InitTargetBuffersIndex);
			if (ease == Ease.InBack)
			{
				targetCore.easeOvershootOrAmplitude = 0.6f;
			}
		}
	}

	private void OnDisable()
	{
		ReleaseBuffers();
		if (positionsBuffer != null)
		{
			positionsBuffer.Release();
			positionsBuffer = null;
		}
	}

	void ReleaseBuffers()
	{

		if (vertexBuffer != null)
		{
			vertexBuffer.Release();
			vertexBuffer = null;
		}

		if (trianglesBuffer != null)
		{
			trianglesBuffer.Release();
			trianglesBuffer = null;
		}
		
		if (totalArea != null)
		{
			totalArea.Release();
			totalArea = null;
		}
		
		if (triAngle != null)
		{
			triAngle.Release();
			triAngle = null;
		}
	}

	private void CalculateTransitioning()
	{
		var numPerTri = (int)(resolution * resolution / (trianglesBuffer.count * 0.33333f));
		groupX = Mathf.CeilToInt(trianglesBuffer.count  * 0.033333f);
		groupY = Mathf.CeilToInt(numPerTri * 0.1f);
		computeShader.SetFloat(TransitionProgressId, transitionDuration);
		computeShader.SetInt(PointsPerTriangleId, numPerTri);
		computeShader.Dispatch(TransformKernelIndex, groupX, groupY, 1);
		
		var totalCount = numPerTri * (trianglesBuffer.count * 0.33333f) - 1;
		var progress = (transitionDuration - 0.5f) * 4f;
		if (totalCount < totalPointsCount)
		{
			if (transitionDuration * 4f <= 1)
			{
				totalPointsCount = (int)Mathf.Lerp(totalPointsCount, (int)totalCount, transitionDuration * 4f);
			}
		}
		else
		{
			if (transitionDuration >= 0.5f && progress <= 1)
			{
				totalPointsCount = (int)Mathf.Lerp(totalPointsCount, (int)totalCount, progress);
			}
		}

		vertexCount = Mathf.Lerp(vertexCount, vertexBuffer.count, transitionDuration);

		var lerp = Mathf.Lerp(300, 250, resolution / vertexCount);
		step = 2f / lerp;
		material.SetBuffer(PositionsId, positionsBuffer);
		material.SetFloat(StepId, step);
	}

	void Update()
	{
		var bounds = new Bounds(Vector3.zero, Vector3.one * 0);
		Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, totalPointsCount);
	}
}