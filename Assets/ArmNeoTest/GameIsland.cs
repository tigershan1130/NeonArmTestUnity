using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Burst;
using static Unity.Burst.Intrinsics.Arm.Neon;
using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.Collections;
using System.Runtime.CompilerServices;

[BurstCompile]
public class GameIsland : MonoSingleton<GameIsland>
{
    public GameObject ObjectToSpawn;
    public float RateOfSpawn = 1;
    public float size = 100;

    // amount must be bigger 8(64bit to store 8 bytes booleans). and must be power based 2. Thus, we don't need to any trailing code for neon.
    public int maxAmount = 1024;
    public int currentAmount = 0;
    private float nextSpawn = 0;
    public float constMoveSpeed = 0.1f;

    // Update is called once per frame
    [System.NonSerialized]
    public GameObject[] SpawnedObjs;

    [System.NonSerialized]
    public float3[] targetPoints;

    [System.NonSerialized]
    public float3[] positions;

    [System.NonSerialized]
    public Material[] dynamicMats;


    // CALCLUATING AND RESULT DATA using computations
    public float[] dynamicFloats;
    
    [System.NonSerialized]
    public bool[] collidedData;


    System.Diagnostics.Stopwatch collisionTimer;

    private float elapsedTime = 0;

    public float averageElapsedTime = 0;

    private int counter;

    public bool NeonTest = false;

    public UnityEngine.UI.Text text;

    public UnityEngine.UI.Text debugUI;

    void Start()
    {
        collisionTimer = new System.Diagnostics.Stopwatch();

        SpawnedObjs = new GameObject[maxAmount];
        targetPoints = new float3[maxAmount];
        positions = new float3[maxAmount];
                   
        collidedData = new bool[maxAmount * maxAmount];

        dynamicMats = new Material[maxAmount];

        for (int i = 0; i < maxAmount; i++)
        {
            nextSpawn = Time.time + RateOfSpawn;

            // Random position within this transform
            Vector3 rndPosWithin;
            rndPosWithin = new Vector3(UnityEngine.Random.Range(-size, size), UnityEngine.Random.Range(-size, size), UnityEngine.Random.Range(-size, size));

            SpawnedObjs[i] = Instantiate(ObjectToSpawn, rndPosWithin, transform.rotation);
            targetPoints[i] = GenerateTargetPoint(rndPosWithin);
            currentAmount = i;
            collidedData[i] = false;
            dynamicMats[i] = SpawnedObjs[i].GetComponent<MeshRenderer>().material;
        }
    }

    private float3 GenerateTargetPoint(Vector3 currentPosition)
    {
        return new Vector3(UnityEngine.Random.Range(-size, size), UnityEngine.Random.Range(-size, size), UnityEngine.Random.Range(-size, size));
    }


    void FixedUpdate()
    {
        // movement adjustment
        for (int i = 0; i < SpawnedObjs.Length; i++)
        {
            float3 dir = targetPoints[i] - (float3)SpawnedObjs[i].transform.position;
            dir = math.normalize(dir);

            SpawnedObjs[i].transform.position = SpawnedObjs[i].transform.position + (Vector3)(dir * constMoveSpeed * Time.fixedDeltaTime);
            positions[i] = SpawnedObjs[i].transform.position;
            if (Vector3.Distance(SpawnedObjs[i].transform.position, (Vector3)targetPoints[i]) < 0.1f)
            {
                targetPoints[i] = GenerateTargetPoint(SpawnedObjs[i].transform.position);
            }

            collidedData[i] = false;
        }

        SetupData(maxAmount, positions);

        collisionTimer.Reset();
        // collision detection
        collisionTimer.Start();

        DoCollisionDetection();

        //string debugText = "";
        //Setting color

        for (int i = 0; i < collidedData.Length; i += maxAmount)
        {

            int length = i + maxAmount;

            int currentChar = i / maxAmount;

            bool isColliding = false;
            // debugText += currentChar.ToString() + " : ";

            for (int c = i; c < length; c += 1)
            {
                int remainder = c % maxAmount;
                isColliding |= collidedData[c] & (currentChar != remainder);
                //debugText += (collidedData[c] && (currentChar != remainder)).ToString() + "(" + collidedData[c].ToString() + ")" + "(" + (currentChar != remainder)  + ") ";
            }

            //debugText += "\n";

            if (isColliding)
            {
                dynamicMats[currentChar].SetColor("_BaseColor", Color.red);
            }
            else
            {
                dynamicMats[currentChar].SetColor("_BaseColor", Color.green);
            }
        }




        collisionTimer.Stop();

        counter++;

        elapsedTime += collisionTimer.ElapsedMilliseconds;

        averageElapsedTime = elapsedTime / counter;

        if(text)
            text.text = "Avg. ms per frame: " + averageElapsedTime.ToString();

        //if(counter == 150)
        //{
        //    if(debugUI)
        //    {
        //        debugUI.text = debugText;

        //    }

        //    Time.timeScale = 0;
        //}

    }


    public unsafe void DoCollisionDetection()
    {
        if (!NeonTest)
        {
            fixed(float* charFloats = dynamicFloats)
            fixed(bool* collisionResult = collidedData)
            {            
                TsAABBIntersect(maxAmount, charFloats, collisionResult);
            }
        }
        else
        {
            fixed(float* charFloats = dynamicFloats)
            fixed(bool* collisionResult = collidedData)
            {            
                TsNeonAABBObjCollisionDetectionUnrolled(maxAmount, charFloats, collisionResult);
            }


        }
    }


    public void SetupData(int numOfCharacters, float3[] positions)
    {
        dynamicFloats = new float[numOfCharacters * 6];//[min.x, min.y, min.z, max.x, max.y, max.z] for each 3d character

        for(int i = 0; i <numOfCharacters; i++)
        {
            dynamicFloats[i] = positions[i].x - 0.5f;
            dynamicFloats[i + numOfCharacters] = positions[i].y - 0.5f;
            dynamicFloats[i + 2 * numOfCharacters] = positions[i].z - 0.5f;

            dynamicFloats[i + 3 * numOfCharacters] = positions[i].x + 0.5f;
            dynamicFloats[i + 4 * numOfCharacters] = positions[i].y + 0.5f;
            dynamicFloats[i + 5 * numOfCharacters] = positions[i].z + 0.5f;
        }

    }

    /// <summary>
    /// TsAABBIntersection
    /// </summary>
    /// <param name="numberCharacters"></param>
    /// <param name="characters"></param>
    /// <param name="collisionResult"></param>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]    
    static unsafe void TsAABBIntersect(int numberCharacters, [NoAlias] in float* characters, [NoAlias] bool* collisionResult)
    {
        // AABB collision detection:
        for (int i = 0; i < numberCharacters; i++)
        {
            float aMinX = characters[i];
            float aMinY = characters[i + numberCharacters];
            float aMinZ = characters[i + 2 * numberCharacters];

            float aMaxX = characters[i + 3 * numberCharacters];
            float aMaxY = characters[i + 4 * numberCharacters];
            float aMaxZ = characters[i + 5 * numberCharacters];

            for (int j = 0; j < numberCharacters; j++)
            {
                //if (i == j) // we don't need to calculate the same.
                //    continue;

                float bMinX = characters[j];
                float bMinY = characters[j + numberCharacters];
                float bMinZ = characters[j + 2 * numberCharacters];

                float bMaxX = characters[j + 3 * numberCharacters];
                float bMaxY = characters[j + 4 * numberCharacters];
                float bMaxZ = characters[j + 5 * numberCharacters];

                bool collision =  (aMinX <= bMaxX
                && aMaxX >= bMinX
                && aMinY <= bMaxY
                && aMaxY >= bMinY
                && aMinZ <= bMaxZ
                && aMaxZ >= bMinZ);

                // optimization can be done here.          
                collisionResult[i * numberCharacters + j] = collision;      
            }
        }



    }

    /// <summary>
    /// Neon Burst implementation of AABB collision detection
    /// </summary>
    /// <param name="numCharacters"></param>
    /// <param name="characters"></param>
    /// <param name="collisions"></param>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static unsafe void TsNeonAABBObjCollisionDetectionUnrolled(int numCharacters, [NoAlias] in float* characters, [NoAlias] bool* collisions)
    {        
        if (IsNeonSupported)
        {
            // vdupq_n_f32: Equivalent Arm instruction: DUP Vd.4S,rn  v128 vdupq_n_f32(float a0) this means v128 register has 4 same 32 bit
            // vld1q_f32: Equivalent Arm instruction: LD1 {Vt.4S},[Xn] Load multiple single - element using pointer into v128 register
            // vqtbl1_u8: this perform a lookups, if the lookup is out of range it is set to 0, else sets results to 1
            // vorrq_u32: 
            // vcgeq_f32: Equivalent Arm instruction: FCMGE Vd.4S,Vm.4S,Vn.4S,  vcgeq_f32(v128, v128) compare 4 32 floats at sametime
            //            Check if it is greater or equal to. if the value is greater than or equal to the corresponding floating-point value in the second source SIMD&FP 
            //            register sets every bit of the corresponding vector element in the destination SIMD&FP register to one, otherwise sets every bit of the corresponding 
            //            vector element in the destination SIMD&FP register to zero

            int c = 0;

            // we iterate through all the number of characters
            for (; c < numCharacters; ++c)
            {
                var tblindex1 = new Unity.Burst.Intrinsics.v64((byte)0, 4, 8, 12, 255, 255, 255, 255);//255=> out of range index will give 0
                var tblindex2 = new Unity.Burst.Intrinsics.v64((byte)255, 255, 255, 255, 0, 4, 8, 12);

                // pointer is getting deferenced, thus this reflect our registers of 128 of 4 32bits are the same, since register doesn't have memory pointer access
                // thus vudpq_n_f32(0f) would return [0f,0f,0f,0f] 128 bits.
                var objAMinX = vdupq_n_f32(*(characters + c));
                var objAMinY = vdupq_n_f32(*(characters + numCharacters + c));
                var objAMinZ = vdupq_n_f32(*(characters + 2 * numCharacters + c));

                var objAMaxX = vdupq_n_f32(*(characters + 3 * numCharacters + c));
                var objAMaxY = vdupq_n_f32(*(characters + 4 * numCharacters + c));
                var objAMaxZ = vdupq_n_f32(*(characters + 5 * numCharacters + c));

                int w = 0;

                // unroll x4 for the second character loops
                // our optmization should be atleast 2 times faster than original with out unrolling.
                // we check 8 characters
                for (; w < (numCharacters & ~7); w+=4)
                {
                    var objBMinX = vld1q_f32(characters + w);
                    var objBMinY = vld1q_f32(characters + numCharacters + w);
                    var objBMinZ = vld1q_f32(characters + numCharacters * 2 + w);

                    var objBMaxX = vld1q_f32(characters + numCharacters * 3 + w);
                    var objBMaxY = vld1q_f32(characters + numCharacters * 4 + w);
                    var objBMaxZ = vld1q_f32(characters + numCharacters * 5 + w);

                    // 0-4
                    //bool collision = (aMinX <= bMaxX
                    //                  && aMaxX >= bMinX
                    //                  && aMinY <= bMaxY
                    //                  && aMaxY >= bMinY
                    //                  && aMinZ <= bMaxZ
                    //                  && aMaxZ >= bMinZ);

                    var greaterEqualABx = vcgeq_f32(objAMaxX, objBMinX);
                    var greaterEqualABy = vcgeq_f32(objAMaxY, objBMinY);
                    var greaterEqualABz = vcgeq_f32(objAMaxZ, objBMinZ);

                    var greaterEqualBAx = vcgeq_f32(objBMaxX, objAMinX);
                    var greaterEqualBAy = vcgeq_f32(objBMaxY, objAMinY);
                    var greaterEqualBAz = vcgeq_f32(objBMaxZ, objAMinZ);


                    // result is 128 bit register with 4 32 bits
                    // each 32 bits can be either 1 or 0 depends on the bitwise logic operation.
                    var result = vandq_u32(vandq_u32(vandq_u32(greaterEqualABx, greaterEqualABy), vandq_u32(greaterEqualABz, greaterEqualBAx)), vandq_u32(greaterEqualBAy, greaterEqualBAz));
                    
                    // vqtbl1 sets each 32 bit result into 8 bits(byte) by using lookups                    
                    // we have populate 64 bit register with: [resut1, result2, result3, result4, 255, 255, 255, 255]
                    var squeezedResult = vqtbl1_u8(result, tblindex1);

                    w+=4;
                    // 4-8

                    objBMinX = vld1q_f32(characters + w);
                    objBMinY = vld1q_f32(characters + numCharacters + w);
                    objBMinZ = vld1q_f32(characters + numCharacters * 2 + w);

                    objBMaxX = vld1q_f32(characters + numCharacters * 3 + w);
                    objBMaxY = vld1q_f32(characters + numCharacters * 4 + w);
                    objBMaxZ = vld1q_f32(characters + numCharacters * 5 + w);

                    greaterEqualABx = vcgeq_f32(objAMaxX, objBMinX);
                    greaterEqualABy = vcgeq_f32(objAMaxY, objBMinY);
                    greaterEqualABz = vcgeq_f32(objAMaxZ, objBMinZ);

                    greaterEqualBAx = vcgeq_f32(objBMaxX, objAMinX);
                    greaterEqualBAy = vcgeq_f32(objBMaxY, objAMinY);
                    greaterEqualBAz = vcgeq_f32(objBMaxZ, objAMinZ);

                    result = vandq_u32(vandq_u32(vandq_u32(greaterEqualABx, greaterEqualABy), vandq_u32(greaterEqualABz, greaterEqualBAx)), vandq_u32(greaterEqualBAy, greaterEqualBAz));
                    
                    // vqtbl1 sets each 32 bit result into 8 bits(byte) by using lookups                    
                    // we have populate 64 bit register with: [255, 255, 255, 255, resut1, result2, result3, result4]
                    var squeezedResult2 = vqtbl1_u8(result, tblindex2);
                    
                    // combine our 1 vs 8 collision detections
                    var finalResult = vadd_u8(squeezedResult, squeezedResult2);

                    // cast to collision result.
                    *(Unity.Burst.Intrinsics.v64*)(collisions + (c * numCharacters + w - 4)) = finalResult;
                } // end for loop of 8 characters comparison 

                // the rest of characters to check.
                if (w + 3 < numCharacters)
                {
                    // do normal code to check rest 
                    for(; w < numCharacters; w++)
                    {
                        var objBMinX = vld1q_f32(characters + w);
                        var objBMinY = vld1q_f32(characters + numCharacters + w);
                        var objBMinZ = vld1q_f32(characters + numCharacters * 2 + w);

                        var objBMaxX = vld1q_f32(characters + numCharacters * 3 + w);
                        var objBMaxY = vld1q_f32(characters + numCharacters * 4 + w);
                        var objBMaxZ = vld1q_f32(characters + numCharacters * 5 + w);


                        var greaterEqualABx = vcgeq_f32(objAMaxX, objBMinX);
                        var greaterEqualABy = vcgeq_f32(objAMaxY, objBMinY);
                        var greaterEqualABz = vcgeq_f32(objAMaxZ, objBMinZ);

                        var greaterEqualBAx = vcgeq_f32(objBMaxX, objAMaxX);
                        var greaterEqualBAy = vcgeq_f32(objBMaxY, objAMaxY);
                        var greaterEqualBAz = vcgeq_f32(objBMaxZ, objAMaxZ);


                        var results = vandq_u32(vandq_u32(vandq_u32(greaterEqualABx, greaterEqualABy), vandq_u32(greaterEqualABz, greaterEqualBAx)), vandq_u32(greaterEqualBAy, greaterEqualBAz));
                    
                        // store our collisions results into the last three results
                        collisions[c * numCharacters + w] = vgetq_lane_u32(results, 0) == 0;
                        collisions[c * numCharacters + w+1] =  vgetq_lane_u32(results, 1) == 0;
                        collisions[c * numCharacters + w+2] =  vgetq_lane_u32(results, 2) == 0;
                    }
                }
            }
        }
    }


}

