using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HaltonSampler
{

    public static float RadicalInverse(uint nBase, uint index)
    {
        float Digit, Radical, Inverse;
        Digit = Radical = 1.0f / nBase;
        Inverse = 0.0f;
        while (index > 0)
        {
            // i余Base求出i在"Base"进制下的最低位的数
            // 乘以Digit将这个数镜像到小数点右边
            Inverse += Digit * (index % nBase);
            Digit *= Radical;

            // i除以Base即可求右一位的数
            index /= nBase;
        }
        return Inverse;
    }

}
