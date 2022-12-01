# NeonArmTestUnity

ARM Neon Collision Detection Test with AABB using Unity3D

Axis-aligned bounding boxes (AABB) is one of the quickest algorithm to determine if two objects are colliding one against another.
This Project contains demo testing against 3D AABB collision detection using ARM Neon. The test runs in bruteforce going through N amount 

Test case: running 1536 objects check against one another using bruteforce AABB. So we are running in O(N^2) time complexity. Collision detected will be highlighted in red.

Case 1: without Neon intrincis:
![alt text](https://github.com/tigershan1130/NeonArmTestUnity/blob/main/ScreenShots/AutoVectorizationTest.png)

Case 2: with Neon intrincis, 4x unrolled.
![alt text](https://github.com/tigershan1130/NeonArmTestUnity/blob/main/ScreenShots/WithNeon.png)

On Average, we are getting running twice as faster compare to the none neon intrincis version.

References:
1. “3D Collision Detection - Game Development: MDN.” Game Development | MDN, https://developer.mozilla.org/en-US/docs/Games/Techniques/3D_collision_detection. 
2. Over17. “Over17/Neonintrinsics-Unity.” GitHub, https://github.com/Over17/NeonIntrinsics-Unity. 
3. Documentation – Arm Developer, https://developer.arm.com/documentation/102556/0100. 
4. https://codeantenna.com/a/6FCoR2kO09
5. https://www.youtube.com/watch?v=BpwvXkoFcp8
6. Documentation – Arm Developer, https://developer.arm.com/documentation/den0018/a/NEON-Intrinsics/Using-NEON-intrinsics
7. https://docs.unity3d.com/Packages/com.unity.burst@1.5/manual/index.html
