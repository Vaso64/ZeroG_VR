﻿using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameJam.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public class GrabItem : MonoBehaviour, IGrabbable
    {
        [SerializeField] private bool initialForce;
        [SerializeField] private bool initialTorque;
        
        public new Rigidbody rigidbody { get; protected set; }

        protected virtual void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            
            // Add random force
            if(initialForce)
                rigidbody.AddForce(Random.insideUnitSphere * Random.Range(0.02f, 0.1f), ForceMode.Impulse);
            
            if(initialTorque)
                rigidbody.AddTorque(Random.insideUnitSphere * Random.Range(0.001f, 0.005f), ForceMode.Impulse);
        }
    }
}