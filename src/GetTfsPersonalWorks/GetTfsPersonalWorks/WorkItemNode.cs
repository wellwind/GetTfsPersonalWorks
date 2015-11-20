using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Collections.Generic;

namespace GetTfsPersonalWorks
{
    internal class WorkItemNode : PropertyChangingBase
    {
        private WorkItem workItem;

        public WorkItem WorkItem
        {
            get { return workItem; }
            set
            {
                workItem = value;
                OnPropertyChanged("WorkItem");
            }
        }

        private string relationshipToParent;

        public string RelationshipToParent
        {
            get { return relationshipToParent; }
            set
            {
                relationshipToParent = value;
                OnPropertyChanged("RelationshipToParent");
            }
        }

        private List<WorkItemNode> children;

        public List<WorkItemNode> Children
        {
            get { return children; }
            set
            {
                children = value;
                OnPropertyChanged("Children");
            }
        }
    }
}