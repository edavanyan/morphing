using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotate : MonoBehaviour
{
    [SerializeField] private Transform target;
    private float speedMod = 2.0f;
    private Vector3 point;

    void Start()
    {
        point = target.position + Vector3.up * 1.5f;
        transform.LookAt(point);
    }

    void Update()
    {
        transform.RotateAround(point, new Vector3(0f, 1.0f, 0.0f), 20 * Time.deltaTime * speedMod);
    }
}
