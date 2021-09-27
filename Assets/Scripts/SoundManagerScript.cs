﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManagerScript : MonoBehaviour
{

    public static AudioClip defenderAttack, soldierAttack, spitterAttack;
    static AudioSource audioSrc;

    // Start is called before the first frame update
    void Start()
    {
        defenderAttack = Resources.Load<AudioClip>("Audio/FX/Defender_Attack");
        soldierAttack = Resources.Load<AudioClip>("Audio/FX/Soldier_Attack");
        spitterAttack = Resources.Load<AudioClip>("Audio/FX/Spitter_Attack");

        audioSrc = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static void PlayUnitSound (string clip)
    {
        switch (clip) {
            case "Defender_Attack":
                audioSrc.PlayOneShot(defenderAttack);
                break;
            case "Soldier_Attack":
                audioSrc.PlayOneShot(soldierAttack);
                break;
            case "Spitter_Attack":
                audioSrc.PlayOneShot(spitterAttack);
                break;
        }
    }
}