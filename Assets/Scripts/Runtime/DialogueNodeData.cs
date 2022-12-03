using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueNodeData
{
    public string UniqueName;
    public string SpeakerId;
    public string DialogueResponseText;
    public string DialogueText;
    public string Traits;
    public string Conditions;
    public string PreAction;
    public string PostAction;
    public string AudioClip;
    public Vector2 Position;
}
