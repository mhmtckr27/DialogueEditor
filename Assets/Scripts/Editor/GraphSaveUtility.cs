using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Node = UnityEditor.Graphs.Node;

public class GraphSaveUtility
{
    private DialogueGraphView _targetGraphView;
    private DialogueContainer _containerCache;
    
    
    public static GraphSaveUtility GetInstance(DialogueGraphView targetGraphView)
    {
        return new GraphSaveUtility()
        {
            _targetGraphView = targetGraphView
        };
    }
    
    private List<Edge> Edges => _targetGraphView.edges.ToList();
    private List<DialogueNode> Nodes => _targetGraphView.nodes.ToList().Cast<DialogueNode>().ToList();

    public DialogueContainer GetSaveData()
    {
        var dialogueContainer = ScriptableObject.CreateInstance<DialogueContainer>();
        
        if(!SaveNodes(dialogueContainer)) return null;

        SaveExposedProperties(dialogueContainer);

        return dialogueContainer;
    }
    
    public void SaveGraph(string fileName, bool tempFile = false)
    {
        var dialogueContainer = GetSaveData();
        
        if(dialogueContainer == null)
            return;

        string path = "Assets/Resources";
        if (tempFile)
            path += "/Tmp";

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Resources", "Tmp");
            
        }
        
        AssetDatabase.CreateAsset(dialogueContainer, $"{path}/{fileName}.asset");
        AssetDatabase.SaveAssets();
    }

    private void SaveExposedProperties(DialogueContainer dialogueContainer)
    {
        dialogueContainer.ExposedProperties.AddRange(_targetGraphView.ExposedProperties);
    }

    private bool SaveNodes(DialogueContainer dialogueContainer)
    {
        if(!Edges.Any()) return false;

        var connectedPorts = Edges.Where(x => x.input.node != null).ToArray();

        for (int i = 0; i < connectedPorts.Length; i++)
        {
            var outputNode = connectedPorts[i].output.node as DialogueNode;
            var inputNode = connectedPorts[i].input.node as DialogueNode;
            
            dialogueContainer.NodeLinks.Add(new NodeLinkData()
            {
                BaseNodeUniqueName = outputNode.UniqueName,
                PortName = connectedPorts[i].output.portName,
                TargetNodeUniqueName = inputNode.UniqueName
            });
        }

        foreach (DialogueNode dialogueNode in Nodes.Where(x => !x.EntryPoint))
        {
            dialogueContainer.DialogueNodeData.Add(new DialogueNodeData()
            {
                UniqueName = dialogueNode.UniqueName,
                SpeakerId = dialogueNode.SpeakerId,
                DialogueResponseText = dialogueNode.DialogueResponseText,
                DialogueText = dialogueNode.DialogueText,
                Traits = dialogueNode.Traits,
                Conditions = dialogueNode.Conditions,
                PreAction = dialogueNode.PreAction,
                PostAction = dialogueNode.PostAction,
                AudioClip = dialogueNode.AudioClip,
                Position = dialogueNode.GetPosition().position
            });
        }
        
        return true;
    }

    public void LoadGraph(string fileName)
    {
        _containerCache = Resources.Load<DialogueContainer>(fileName);

        if (_containerCache == null)
        {
            EditorUtility.DisplayDialog("File Not Found", "Target dialogue graph file does not exist!", "OK");
            return;
        }

        ClearGraph(_containerCache);
        LoadExposedProperties(_containerCache, false);
        CreateNodes(_containerCache);
        ConnectNodes(_containerCache);
    }

    private void LoadExposedProperties(DialogueContainer container, bool reload = false)
    {
        _targetGraphView.ClearBlackBoardAndExposedProperties();

        for (int i = 0; i < container.ExposedProperties.Count; i++)
        {
            if (reload && i == container.ExposedProperties.Count - 1)
                _targetGraphView.AddPropertyToBlackBoard( container.ExposedProperties[i], true);
            else
                _targetGraphView.AddPropertyToBlackBoard( container.ExposedProperties[i], false);
        }
    }

    private void ConnectNodes(DialogueContainer container)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {            
            var outputPorts = Nodes[i].outputContainer.Children().Where(x => x.GetType().Name.Contains("Port")).ToList();

            if(outputPorts.Count <= 0)
                continue;
            var connections = container.NodeLinks.Where(x => x.BaseNodeUniqueName == Nodes[i].UniqueName).ToList();
            for (int j = 0; j < connections.Count; j++)
            {
                var targetNodeGuid = connections[j].TargetNodeUniqueName;
                var targetNode = Nodes.First(x => x.UniqueName == targetNodeGuid);
                
                LinkNodes(outputPorts[j].Q<Port>(), (Port) targetNode.inputContainer[0]);
                
                targetNode.SetPosition(new Rect(container.DialogueNodeData.First(x => x.UniqueName == targetNodeGuid).Position, _targetGraphView.nodeSize));
            }
        }
    }

    private void LinkNodes(Port output, Port input)
    {
        try
        {
            var tempEdge = new Edge()
            {
                output = output,
                input = input
            };

            tempEdge?.input.Connect(tempEdge);
            tempEdge?.output.Connect(tempEdge);
            _targetGraphView.Add(tempEdge);

        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    private void CreateNodes(DialogueContainer container)
    {
        foreach (DialogueNodeData nodeData in container.DialogueNodeData)
        {
            var tempNode = _targetGraphView.CreateDialogueNode(nodeData, Vector2.zero, false);

            _targetGraphView.AddElement(tempNode);

            var nodePorts = container.NodeLinks.Where(x => x.BaseNodeUniqueName == nodeData.UniqueName).ToList();
            nodePorts.ForEach(x => _targetGraphView.AddChoicePort(tempNode, x.PortName));
            
        }
    }

    private void ClearGraph(DialogueContainer container)
    {
        //Set entry point's guid back from the save. Discard existing guid.
        Nodes.Find(x => x.EntryPoint).UniqueName = container.NodeLinks[0].BaseNodeUniqueName;

        foreach (DialogueNode node in Nodes)
        {
            if(node.EntryPoint) 
                continue;
            
            //Remove edges that connected to this node
            Edges.Where(x => x.input.node == node).ToList().ForEach(edge => _targetGraphView.RemoveElement(edge));
            
            //Then remove the node
            _targetGraphView.RemoveElement(node);
            
            
        }
    }
}
