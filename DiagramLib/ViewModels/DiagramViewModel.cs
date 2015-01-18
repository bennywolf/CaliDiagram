﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;

namespace DiagramLib.ViewModels
{
    public class DiagramViewModel: PropertyChangedBase
    {
        
        public DiagramViewModel(DiagramDefinitionBase diagramDefinition)
        {
            Nodes = new ObservableCollection<NodeBaseViewModel>();
            Edges = new ObservableCollection<ConnectionViewModel>();
            AttachDescriptors = new ObservableCollection<NodeBaseViewModel>();
            this.Definition = diagramDefinition;
        }
        public DiagramDefinitionBase Definition
        {
            get;
            private set;
        }

        public List<string> Commands
        {
            get
            {
                List<string> commands = new List<string>();
                commands.Add("move");
                commands.Add("add_connection");
                commands.AddRange(Definition.nodeBehaviours.Select(nb => "add_" + nb.Key).ToList());

                commands.Add("remove");
                return commands;
            }
        }
        public string SelectedCommand
        {
            get;
            set;
        }
        public IList<NodeBaseViewModel> AttachDescriptors { get; private set; }
        public IList<NodeBaseViewModel> Nodes { get; private set; }
        public IList<ConnectionViewModel> Edges { get; private set; }
        public event EventHandler<Point> OnDiagramClick;
        public event EventHandler<NodeBaseViewModel> NodeSelected;
        public event EventHandler<ConnectionViewModel> ConnectionSelected;

        public void ForceRedraw()
        {
            Application.Current.Dispatcher.Invoke(new System.Action(() => { }), DispatcherPriority.ContextIdle, null);
        }


        void HandleClickCommand(Point pos)
        {
            if (string.IsNullOrEmpty(SelectedCommand))
                return;
            

            
            else if(SelectedCommand.StartsWith("add_"))
            {
                string nodeTag = SelectedCommand.Substring("add_".Length);

                NodeBehaviour beh = null;
                if(!Definition.nodeBehaviours.TryGetValue(nodeTag, out beh))
                    return;

                var vm =  Definition.nodeBehaviours[nodeTag].CreateNode(pos);
                if (vm == null)
                    return;
                AddNode(vm, pos);
            }
        }
        private NodeBaseViewModel prevSelectedNode = null;
        void HandleSelectNodeCommand(NodeBaseViewModel node)
        {
            if(SelectedCommand == "add_connection")
            {
                if (prevSelectedNode != null)
                {
                    AddConnection(prevSelectedNode, node);
                    prevSelectedNode = null;
                }
                else
                {
                    prevSelectedNode = node;
                }
            }
            if(SelectedCommand == "remove")
            {
                RemoveNode(node);
            }
        }

        public void DiagramClick(MouseButtonEventArgs args, FrameworkElement el)
        {
            
           var pos =  args.GetPosition(el);
           HandleClickCommand(pos);
            if (OnDiagramClick != null)
                OnDiagramClick(this, pos);
        }


        private NodeBaseViewModel _SelectedNode;
        public NodeBaseViewModel SelectedNode
        {
            get { return _SelectedNode; }
            set
            {
                if (_SelectedNode != value)
                {
                    _SelectedNode = value;
                    NotifyOfPropertyChange(() => SelectedNode);
                }
            }
        }

        public bool IsInBatchMode { get; internal set; }

        public void AddNode(NodeBaseViewModel node, Point location)
        {
            if (node.ParentDiagram != null)
                throw new InvalidOperationException("Node is already added to diagram");
            node.ParentDiagram = this;
            node.Location = location;
            Nodes.Add(node);
            ForceRedraw();
            node.RaiseInitialize();
        }
        public void SelectNode(NodeBaseViewModel vm)
        {
            HandleSelectNodeCommand(vm);
            SelectedNode = vm;
            if (NodeSelected != null)
                NodeSelected(this, vm);
        }

        public void RemoveNode(NodeBaseViewModel node)
        {
            node.ParentDiagram = null;
            Nodes.Remove(node);
            
            var edgesToRemove = Edges.Where(edge => edge.From == node || edge.To == node).ToList();
            foreach (var edge in edgesToRemove)
            {
                RemoveConnection(edge);           
            }
            node.Dispose();
        }

        
        
        
        

        public ConnectionViewModel AddConnection(NodeBaseViewModel from, NodeBaseViewModel to)
        {
            var edge = Definition.CreateConnection(from, to);

            // prevent showing attach descriptors  when their position is not calculated yet
            // this can be possible not necessary if calculation their location was done before data binding of AttachDescriptors collection
            // but then we would not have size of attach descriptors and size is necessary for calculating position
            if(edge.FromDescriptor != null)
                edge.FromDescriptor.Location = new Point(-1000, -1000);
            if(edge.ToDescriptor != null)
                edge.ToDescriptor.Location = new Point(-1000, -1000);

            edge.ParentDiagram = this;


            if (edge.FromDescriptor != null)
            {
                AttachDescriptors.Add(edge.FromDescriptor);
                
                edge.FromDescriptor.Name = "from";

                if (!IsInBatchMode)
                {
                    ForceRedraw();
                    edge.FromDescriptor.RaiseInitialize();
                }
            }
            if (edge.ToDescriptor != null)
            {
                AttachDescriptors.Add(edge.ToDescriptor);

                edge.ToDescriptor.Name = "to";
                if (!IsInBatchMode)
                {
                    ForceRedraw();
                    edge.ToDescriptor.RaiseInitialize();
                }
            }
            if (!IsInBatchMode)
                ForceRedraw();

            
            
            Edges.Add(edge);

            var sides = Definition.ConnectorSideStrategy.GetSidesForConnection(edge);
            edge.AttachPointFrom = from.Attach(sides.FromSide, edge, edge.FromDescriptor);
            edge.AttachPointTo = to.Attach(sides.ToSide, edge, edge.ToDescriptor);

            if (!IsInBatchMode)
                edge.UpdateConnection();
            from.RaiseConnectionAdded(edge);
            to.RaiseConnectionAdded(edge);
            return edge;
        }

        public void SelectConnection(ConnectionViewModel edge)
        {
            if (SelectedCommand == "remove")
                RemoveConnection(edge);
            else
            {
                if (ConnectionSelected != null)
                    ConnectionSelected(this, edge);
            }
        } 

        public void RemoveConnection(ConnectionViewModel edge)
        {
            edge.Dispose();
            if (edge.FromDescriptor != null)
                AttachDescriptors.Remove(edge.FromDescriptor);
            if (edge.ToDescriptor != null)
                AttachDescriptors.Remove(edge.ToDescriptor);
            Edges.Remove(edge);
            edge.From.RaiseConnectionRemoved(edge);
            edge.To.RaiseConnectionRemoved(edge);
        }
        public void ClearDiagram()
        {
            foreach (var edge in Edges)
                edge.Dispose();
            foreach (var node in Nodes)
                node.Dispose();
            foreach (var node in AttachDescriptors)
                node.Dispose();
            Edges.Clear();
            AttachDescriptors.Clear();
            Nodes.Clear();
        }
    }

}
