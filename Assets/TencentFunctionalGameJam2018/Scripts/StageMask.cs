﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageMask : MonoBehaviour
{
    public void PlayLight()
    {
        GetComponent<Animator>().Play("Light");
    }
    public void PlayLightFinal()
    {
        GetComponent<Animator>().Play("Light_Final");
    }
}
