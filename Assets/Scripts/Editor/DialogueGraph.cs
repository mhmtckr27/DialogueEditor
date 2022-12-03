using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueGraph : EditorWindow
{
    public static DialogueGraphView _graphView;
    private string _fileName = "New Narrative";
    
    private MiniMap miniMap;

    private string lastTempFileName;
    
    [MenuItem("Dialogue/Dialogue Graph")]
    public static void OpenDialogueGraphWindow()
    {
        var window = GetWindow<DialogueGraph>();
        window.titleContent = new GUIContent("Dialogue Graph");
    }

    private void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
        GenerateMinimap();
        GenerateBlackBoard();
    }

    private void GenerateBlackBoard()
    {
        var blackBoard = new Blackboard(_graphView);
        blackBoard.Add(new BlackboardSection(){ title = "Characters"});
        blackBoard.addItemRequested = _blackBoard =>
        {
            _graphView.AddPropertyToBlackBoard(new ExposedProperty(), true);
        };

        blackBoard.editTextRequested = (blackboard1, element, newValue) =>
        {
            var oldPropertyName = ((BlackboardField) element).text;
            if (_graphView.ExposedProperties.Any(x => x.PropertyName == newValue))
            {
                EditorUtility.DisplayDialog("Duplicate Property!", $"Property with name: {newValue} already defined!",
                    "OK");
                return;
            }

            var propertyIndex = _graphView.ExposedProperties.FindIndex(x => x.PropertyName == oldPropertyName);
            _graphView.ExposedProperties[propertyIndex].PropertyName = newValue;
            ((BlackboardField) element).text = newValue;
            _graphView.RefreshSpeakerPopup();
        };

        blackBoard.SetPosition(new Rect(10, 30, 200, 300));
        _graphView.Add(blackBoard);
        _graphView.Blackboard = blackBoard;
    }

    private void GenerateMinimap()
    {
        miniMap = new MiniMap
        {
            anchored = true
        };
        RefreshMinimapPosition();
        _graphView.Add(miniMap);
    }

    public void RefreshMinimapPosition()
    {
        miniMap.SetPosition(new Rect(position.width - 210, 30, 200, 140));
    }

    private void ConstructGraphView()
    {
        _graphView = new DialogueGraphView(this)
        {
            name = "Dialogue Graph",
        };
        
        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);
    }

    private void GenerateToolbar()
    {
        var toolbar = new Toolbar();

        var fileNameTextField = new TextField("File Name:");
        fileNameTextField.SetValueWithoutNotify(_fileName);
        fileNameTextField.MarkDirtyRepaint();
        fileNameTextField.RegisterValueChangedCallback(evt => _fileName = evt.newValue);
        toolbar.Add(fileNameTextField);
        
        toolbar.Add(new Button((() => RequestDataOperation(true))){text = "Save Data"});
        toolbar.Add(new Button((() => RequestDataOperation(false))){text = "Load Data"});

        var refreshMinimapPositionButton = new Button(RefreshMinimapPosition);
        refreshMinimapPositionButton.text = "Refresh Minimap";
        toolbar.Add(refreshMinimapPositionButton);

        var exportToCSVButton = new Button(ExportToCSV);
        exportToCSVButton.text = "Export";
        toolbar.Add(exportToCSVButton);
        
        rootVisualElement.Add(toolbar);
    }
    public static string csvPath = "/DT_Dialogue.csv";
    
    public void ExportToCSV()
    {
        string filePath = Application.dataPath + csvPath;
        List<string> lines = new List<string>();
        lines.Add("Name,SpeakerId,Text,ResponseText,Branches,Traits,Conditions,PreAction,PostAction,AudioClip");
        
        foreach (var node in _graphView.nodes.ToList().Cast<DialogueNode>())
        {
            if(node.DialogueResponseText.Contains("ENTRYPOINT"))
                continue;
            
            string branches = "";
            
            var outputPorts = node.outputContainer.Children().Where(x => x.GetType().Name.Contains("Port")).ToList();

            for (int i = 0; i < outputPorts.Count; i++)
            {
                if(!outputPorts[i].Q<Port>().connected)
                    continue;

                branches += ((DialogueNode) outputPorts[i].Q<Port>().connections.First().input.node).UniqueName + ";";
            }

            string line = $"{node.UniqueName},{node.SpeakerId},\"{node.DialogueText}\",\"{node.DialogueResponseText}\"," +
                          $"{branches},{node.Traits},{node.Conditions},{node.PreAction},{node.PostAction},{node.AudioClip}";
            lines.Add(line);
        }
        
        File.WriteAllLines(filePath, lines);
    }
    
    private void RequestDataOperation(bool save)
    {
        if (string.IsNullOrEmpty(_fileName))
        {
            EditorUtility.DisplayDialog("Invalid file name!", "Please enter a valid file name.", "OK");
        }

        var saveUtility = GraphSaveUtility.GetInstance(_graphView);
        
        if(save)
            saveUtility.SaveGraph(_fileName);
        else
            saveUtility.LoadGraph(_fileName);
    }
    
    private void OnDisable()
    {
        rootVisualElement.Remove(_graphView);
    }
}