using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TestDistance : MonoBehaviour
{
    public Vector3 StartPos;
    public Vector3 StartDir;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    float distanceToIntersection(Vector3 origin, Vector3 dir, Vector3 v)
    {
        float numer;
        float denom = v.y * dir.z - v.z * dir.y;

        if (Mathf.Abs(denom) > 0.1f)
        {
            numer = origin.y * dir.z - origin.z * dir.y;
        }
        else
        {
            // We're in the yz plane; use another one
            numer = origin.x * dir.y - origin.y * dir.x;
            denom = v.x * dir.y - v.y * dir.x;
        }

        return numer / denom;
    }

    // Update is called once per frame
    void Update()
    {
        float dist = distanceToIntersection(StartPos - transform.position, StartDir.normalized, Vector3.down);
        Debug.Log(dist.ToString());
    }
}
