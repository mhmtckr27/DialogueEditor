using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Codice.Client.BaseCommands.BranchExplorer;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class DialogueGraphView : GraphView, IEdgeConnectorListener
{
    public static DialogueGraphView Instance; 
    public readonly Vector2 nodeSize = new Vector2(150, 200);
    public Blackboard Blackboard;
    
    public List<ExposedProperty> ExposedProperties = new List<ExposedProperty>();
    
    private NodeSearchWindow _searchWindow;
    private EditorWindow _window;

    public DialogueGraphView(EditorWindow window)
    {
        _window = window;
        styleSheets.Add(Resources.Load<StyleSheet>("DialogueGraph"));
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        ShortcutDelegate addDelegate = () =>
        {
            var worldMousePos = _window.rootVisualElement.ChangeCoordinatesTo(_window.rootVisualElement.parent,
                Event.current.mousePosition);

            var localMousePos = contentViewContainer.WorldToLocal(worldMousePos);
            CreateNode("Dialogue Node", localMousePos);
            return EventPropagation.Continue;
        };

        var a = new Dictionary<Event, ShortcutDelegate>();
        a.Add(Event.KeyboardEvent("E"), addDelegate);
        
        this.AddManipulator(new ShortcutHandler(a));

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();
        
        AddElement(GenerateEntryPointNode());
        AddSearchWindow();
    }

    
    private void AddSearchWindow()
    {
        _searchWindow = ScriptableObject.CreateInstance<NodeSearchWindow>();
        _searchWindow.Init(this, _window);
        nodeCreationRequest = context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatiblePorts = new List<Port>();

        ports.ForEach((port) =>
        {
            if(startPort != port && startPort.node != port.node)
                compatiblePorts.Add(port);
        });

        return compatiblePorts;
    }

    private Port GeneratePort(DialogueNode node, Direction portDirection, Port.Capacity capacity = Port.Capacity.Single)
    {
        return node.InstantiatePort(Orientation.Horizontal, portDirection, capacity, typeof(float));
    }

    private DialogueNode GenerateEntryPointNode()
    {
        var node = new DialogueNode()
        {
            title = "START",
            UniqueName = "ENTRYPOINT",
            DialogueResponseText = "ENTRYPOINT",
            EntryPoint = true
        };

        var generatedPort = GeneratePort(node, Direction.Output);
        generatedPort.AddManipulator(new EdgeConnector<Edge>(this));

        generatedPort.portName = "Next";
        node.outputContainer.Add(generatedPort);

        node.capabilities &= ~Capabilities.Movable;
        node.capabilities &= ~Capabilities.Deletable;

        node.RefreshExpandedState();
        node.RefreshPorts();
        
        node.SetPosition(new Rect(100, 200, 100, 150));
        return node;
    }

    public DialogueNode CreateNode(string nodeName, Vector2 mousePos)
    {
        var node = CreateDialogueNode(new DialogueNodeData(), mousePos);
        AddElement(node);
        return node;
    }

    public DialogueNode CreateDialogueNode(DialogueNodeData original, Vector2 nodePos, bool useDefaultValues = true)
    {
        var dialogueNode = new DialogueNode(original);

        var inputPort = GeneratePort(dialogueNode, Direction.Input, Port.Capacity.Multi);
        inputPort.portName = "";
        dialogueNode.inputContainer.Add(inputPort);
        
        dialogueNode.styleSheets.Add(Resources.Load<StyleSheet>("Node"));
        inputPort.style.color = new StyleColor(new Color(0, 0, 0));
        
        var button = new Button(() =>
        {
            AddChoicePort(dialogueNode);
        });
        button.text = "+";
        dialogueNode.outputContainer.Add(button);

        int defaultIndex = useDefaultValues
            ? 0
            : ExposedProperties.FindIndex(x => x.PropertyName == dialogueNode.SpeakerId);

        defaultIndex = defaultIndex < 0 ? 0 : defaultIndex;

        PopupField<string> speakerIdEnumField =
            new PopupField<string>(ExposedProperties.Select(x => x.PropertyName).ToList(), defaultIndex);
        dialogueNode.titleContainer.Add(speakerIdEnumField);
        speakerIdEnumField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.SpeakerId = evt.newValue;
        }));
        var uniqueNameField = new TextField(string.Empty)
        {
            label = "Name",
            style =
            {
                width = 300,
                whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.NoWrap)
            },
        };
        speakerIdEnumField.SendToBack();
        uniqueNameField.RegisterValueChangedCallback((evt =>
        {
            // dialogueNode.title = evt.newValue;
            dialogueNode.UniqueName = evt.newValue;
        }));
        dialogueNode.UniqueName = useDefaultValues ? Guid.NewGuid().ToString() : dialogueNode.UniqueName;
        uniqueNameField.SetValueWithoutNotify(dialogueNode.UniqueName);
        // dialogueNode.title = useDefaultValues ? "" : dialogueNode.UniqueName;
        dialogueNode.mainContainer.Add(uniqueNameField);
        
        var dialogueTextField = new TextField(string.Empty)
        {
            label = "Response",
            style = { width = 300},
        };
        dialogueTextField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.DialogueResponseText = evt.newValue;
        }));        
        dialogueTextField.SetValueWithoutNotify(dialogueNode.DialogueResponseText);
        dialogueNode.mainContainer.Add(dialogueTextField);

        
        
        var dialogueTextLongField = new TextField(string.Empty)
        {
            label = "Text",
            style = { width = 300 },
            multiline = true
        };
        dialogueTextLongField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.DialogueText = evt.newValue;
        }));
        dialogueTextLongField.SetValueWithoutNotify(dialogueNode.DialogueText);
        dialogueNode.mainContainer.Add(dialogueTextLongField);



        var traitsTextField = new TextField(string.Empty)
        {
            label = "Traits",
            style = { width = 300 }
        };
        traitsTextField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.Traits = evt.newValue;
        }));
        traitsTextField.SetValueWithoutNotify("");
        // dialogueNode.mainContainer.Add(traitsTextField);

        
        
        var conditionsTextField = new TextField(string.Empty)
        {
            label = "Conditions",
            style = { width = 300 }
        };;
        conditionsTextField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.Conditions = evt.newValue;
        }));
        conditionsTextField.SetValueWithoutNotify(dialogueNode.Conditions);
        // dialogueNode.mainContainer.Add(conditionsTextField);

        
        
        var preActionTextField = new TextField(string.Empty)        
        {
            label = "PreAction",
            style = { width = 300 }
        };
        preActionTextField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.PreAction = evt.newValue;
        }));
        preActionTextField.SetValueWithoutNotify(dialogueNode.PreAction);
        // dialogueNode.mainContainer.Add(preActionTextField);

        
        
        var postActionTextField = new TextField(string.Empty)
        {
            label = "PostAction",
            style = { width = 300 }
        };
        postActionTextField.RegisterValueChangedCallback((evt =>
        {
            dialogueNode.PostAction = evt.newValue;
        }));
        postActionTextField.SetValueWithoutNotify(dialogueNode.PostAction);
        // dialogueNode.mainContainer.Add(postActionTextField);

        var container = new VisualElement();
        container.Add(traitsTextField);
        container.Add(conditionsTextField);
        container.Add(preActionTextField);
        container.Add(postActionTextField);
        
        dialogueNode.mainContainer.Add(container);
        dialogueNode.mainContainer.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.7f));
        
        Button collapseButton = new Button();
        collapseButton = new Button(() => ToggleCollapseAdditionals(dialogueNode, collapseButton, container))
        {
            text = "↑",
            style =
            {
                minWidth = 50,
                alignSelf = new StyleEnum<Align>(Align.Center),
                backgroundColor = new StyleColor(Color.black)
            }
        };

        dialogueNode.mainContainer.Add(collapseButton);
        // ToggleCollapseAdditionals(dialogueNode, collapseButton, container);
        
        // asd.Add(traitsTextField);
        // asd.Add(conditionsTextField);
        // asd.Add(preActionTextField);
        // asd.Add(postActionTextField);
        // dialogueNode.mainContainer.Add(asd);
        
        dialogueNode.RefreshExpandedState();
        dialogueNode.RefreshPorts();
        dialogueNode.SetPosition(new Rect(nodePos, nodeSize));


        // if (useDefaultValues)
        // {
        //     AddChoicePort(dialogueNode);
        //     AddChoicePort(dialogueNode);
        //     AddChoicePort(dialogueNode);
        //     AddChoicePort(dialogueNode);
        // }
        
        return dialogueNode;
    }

    private void ToggleCollapseAdditionals(DialogueNode dialogueNode, Button collapseButton, VisualElement container)
    {
        dialogueNode.expanded = !dialogueNode.expanded;
        collapseButton.text = dialogueNode.expanded ? "↑" : "↓";
        container.visible = dialogueNode.expanded;
        UpdateCollapsibleAreaVisibility(dialogueNode.expanded, dialogueNode, container, collapseButton);
    }

    void UpdateCollapsibleAreaVisibility(bool expanded, DialogueNode node, VisualElement collapsibleArea, VisualElement collapseButton)
    {
        var m_CollapsibleArea = collapsibleArea;
        if (m_CollapsibleArea == null)
        {
            return;
        }

        bool displayBottom = expanded;

        if (displayBottom)
        {
            if (m_CollapsibleArea.parent == null)
            {
                VisualElement contents = node.mainContainer;

                if (contents == null)
                {
                    return;
                }

                contents.Add(m_CollapsibleArea);
            }

            m_CollapsibleArea.BringToFront();
            collapseButton.BringToFront();
        }
        else
        {
            if (m_CollapsibleArea.parent != null)
            {
                m_CollapsibleArea.RemoveFromHierarchy();
            }
        }
    }

    public void AddChoicePort(DialogueNode dialogueNode, string overriddenPortName = "")
    {
        var generatedPort = GeneratePort(dialogueNode, Direction.Output);
        generatedPort.AddManipulator(new EdgeConnector<Edge>(this));

        var oldLabel = generatedPort.contentContainer.Q<Label>("type");
        generatedPort.contentContainer.Remove(oldLabel);
        
        var outputPortCount = dialogueNode.outputContainer.Query("connector").ToList().Count;

        var portName = string.IsNullOrEmpty(overriddenPortName) 
            ? $"Choice {outputPortCount + 1}"
            : overriddenPortName;

        var label = new Label(portName);
        
        // var textField = new TextField()
        // {
        //     name = string.Empty,
        //     value = portName
        // };

        // generatedPort.connections.First().input.RegisterCallback();
            
        // textField.RegisterValueChangedCallback(evt => generatedPort.portName = evt.newValue);
        generatedPort.contentContainer.Add(new Label(" "));
        generatedPort.contentContainer.Add(label);
        var deleteButton = new Button((() => RemovePort(dialogueNode, generatedPort)))
        {
            text = "X"
        };

        generatedPort.contentContainer.Add(deleteButton);
        
        generatedPort.portName = portName;
        
        dialogueNode.outputContainer.Add(generatedPort);        
        dialogueNode.RefreshExpandedState();
        dialogueNode.RefreshPorts();
    }

    private void RemovePort(DialogueNode dialogueNode, Port generatedPort)
    {
        var targetEdge = edges.ToList().Where(x =>
            x.output.portName == generatedPort.portName && x.output.node == generatedPort.node);

        if (targetEdge.Any())
        {
            var edge = targetEdge.First();
            edge.input.Disconnect(edge);
            RemoveElement(targetEdge.First());
        }

        dialogueNode.outputContainer.Remove(generatedPort);
        dialogueNode.RefreshPorts();
        dialogueNode.RefreshExpandedState();
    }

    public void OnDropOutsidePort(Edge edge, Vector2 position)
    {            
        var worldMousePos = _window.rootVisualElement.ChangeCoordinatesTo(_window.rootVisualElement.parent,
            Event.current.mousePosition);

        var localMousePos = contentViewContainer.WorldToLocal(worldMousePos);
        
        var tempEdge = new Edge()
        {
            output = edge.output,
            input = (Port)CreateNode("Dialogue Node", localMousePos).inputContainer[0]
        };
        
        tempEdge.input.Connect(tempEdge);
        tempEdge.output.Connect(tempEdge);
        Add(tempEdge);
    }

    public void OnDrop(GraphView graphView, Edge edge)
    {
    }

    public void ClearBlackBoardAndExposedProperties()
    {
        ExposedProperties.Clear();
        Blackboard.Clear();
    }
    
    public void AddPropertyToBlackBoard(ExposedProperty exposedProperty, bool reload = false)
    {
        var localPropertyNameBase = exposedProperty.PropertyName;
        var localPropertyName = exposedProperty.PropertyName;
        // var localPropertyValue = exposedProperty.PropertyValue;
        int count = 1;
        while (ExposedProperties.Any(x => x.PropertyName == localPropertyName))
        {
            localPropertyName = $"{localPropertyName}(a)";
            count++;
        }
        
        var property = new ExposedProperty();
        property.PropertyName = localPropertyName;
        // property.PropertyValue = localPropertyValue;
        ExposedProperties.Add(property);
        
        RefreshSpeakerPopup();

        var container = new VisualElement();
        var blackBoardField = new BlackboardField()
        {
            text = property.PropertyName,
            typeText = "string",
            style =
            {
                borderBottomColor = new StyleColor(Color.black),
                borderBottomWidth = 2
            }
        };
        

        var deletePropertyButton = new Button((delegate
        {
            Blackboard.Remove(container);
            ExposedProperties.Remove(property);
            RefreshSpeakerPopup();
        }));
        deletePropertyButton.text = "X";
        deletePropertyButton.style.maxWidth = 20;
        blackBoardField.Add(deletePropertyButton);
        // container.Add(deletePropertyButton);

        container.Add(blackBoardField);
        // var propertyValueTextField = new TextField("Value: ")
        // {
        //     value = localPropertyValue
        // };
        // propertyValueTextField.RegisterValueChangedCallback(evt =>
        // {
        //     var changingPropertyIndex = ExposedProperties.FindIndex(x => x.PropertyName == property.PropertyName);
        //     ExposedProperties[changingPropertyIndex].PropertyValue = evt.newValue;
        // });

        // var blackBoardValueRow = new BlackboardRow(blackBoardField, deletePropertyButton);
        
        // container.Add(blackBoardValueRow);
        Blackboard.Add(container);
    }

    public void RefreshSpeakerPopup()
    {
        foreach (var node in nodes.ToList().Cast<DialogueNode>())
        {
            if (node.titleContainer.Children().ToList().Any(x => x.GetType() == typeof(PopupField<string>)))
            {
                ((PopupField<string>)node.titleContainer.Children().ToList().First(x => x.GetType() == typeof(PopupField<string>))).choices = ExposedProperties.Select(x => x.PropertyName).ToList();
                node.RefreshExpandedState();
                node.RefreshPorts();
            }
        }
    }
}