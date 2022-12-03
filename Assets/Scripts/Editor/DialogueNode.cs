using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Node = UnityEditor.Experimental.GraphView.Node;

public class DialogueNode : Node
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
    public bool EntryPoint = false;

    public DialogueNode(DialogueNodeData data)
    {
        UniqueName = data.UniqueName;
        SpeakerId = data.SpeakerId;
        DialogueResponseText = data.DialogueResponseText;
        DialogueText = data.DialogueText;
        Traits = data.Traits;
        Conditions = data.Conditions;
        PreAction = data.PreAction;
        PostAction = data.PostAction;
        AudioClip = data.AudioClip;
    }
    
    public DialogueNode(){}
}
