using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Discussion.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetTfsPersonalWorks
{
    public partial class frmMain : Form
    {
        private TfsTeamProjectCollection selectedProjectCollection;

        private int howManyDays = 14;

        public frmMain()
        {
            InitializeComponent();
            TeamProjectPicker projectPicker = new TeamProjectPicker();
            var userSelected = projectPicker.ShowDialog();
            if (userSelected == DialogResult.Cancel)
            {
                return;
            }

            if (projectPicker.SelectedTeamProjectCollection != null)
            {
                Uri tfsUri = projectPicker.SelectedTeamProjectCollection.Uri;
                string teamProjectName = projectPicker.SelectedProjects[0].Name;
                selectedProjectCollection = projectPicker.SelectedTeamProjectCollection;
                this.Text = selectedProjectCollection.DisplayName;
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var workList = new Dictionary<string, MyWorks>();

            var query = selectedProjectCollection.GetService<WorkItemStore>();

            var productBacklogTaskMapping = getProductBacklogTaskMapping(query);

            var relatedLinkIds = productBacklogTaskMapping.Keys.ToList();

            var featureProductBacklogMapping = getFeatureProductBacklogMapping(query, relatedLinkIds);

            var features = getFeatures(query, featureProductBacklogMapping);

            // TODO: output to markdown
            StringBuilder sb = new StringBuilder();
            List<int> addedTasks = new List<int>();
            foreach (var feature in features)
            {
                sb.AppendLine(String.Format("# {0}", feature["System.Title"].ToString()));
                sb.AppendLine("");
                var backlogs = featureProductBacklogMapping[(int)feature["System.Id"]];
                foreach (var backlog in backlogs)
                {
                    var backlogId = (int)backlog["System.Id"];
                    sb.AppendLine(String.Format("## {0}", backlog["System.Title"].ToString()));
                    var tasks = productBacklogTaskMapping[backlogId];
                    foreach (var task in tasks)
                    {
                        sb.AppendLine(String.Format("- {0}", task["System.Title"].ToString()));
                        addedTasks.Add((int)task["System.Id"]);
                    }
                    sb.AppendLine("");
                    productBacklogTaskMapping.Remove(backlogId);
                }
                sb.AppendLine("");
            }

            sb.AppendLine("# 未分類");
            var wiqlBacklogs =
                "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.Id] IN ({0}) AND [System.WorkItemType] = '產品待處理項目'";
            var backlogList = query.Query(String.Format(wiqlBacklogs, String.Join(",", productBacklogTaskMapping.Keys.ToArray())));
            foreach (var backlog in backlogList.Cast<WorkItem>())
            {
                sb.AppendLine(String.Format("## {0}", backlog["System.Title"].ToString()));
                foreach (var tasks in productBacklogTaskMapping[(int)backlog["System.Id"]])
                {
                    sb.AppendLine(String.Format("- {0}", tasks["System.Title"].ToString()));
                }
                sb.AppendLine("");
            }

            System.Diagnostics.Debug.WriteLine(sb.ToString());
        }

        private static List<WorkItem> getFeatures(WorkItemStore query, Dictionary<int, List<WorkItem>> featureProductBacklogMapping)
        {
            List<WorkItem> features = new List<WorkItem>();
            var wiqlFeatures =
                "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.Id] IN ({0}) AND [System.WorkItemType] = '特殊功能'";
            var queryFeatures =
                query.Query(String.Format(wiqlFeatures, String.Join(",", featureProductBacklogMapping.Keys.ToArray())));
            features = queryFeatures.Cast<WorkItem>().ToList();
            return features;
        }

        private static Dictionary<int, List<WorkItem>> getFeatureProductBacklogMapping(WorkItemStore query, List<int> relatedLinkIds)
        {
            Dictionary<int, List<WorkItem>> featureProductBacklogMapping = new Dictionary<int, List<WorkItem>>();

            var wiqlBacklogs =
                "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.Id] IN ({0}) AND [System.WorkItemType] = '產品待處理項目'";
            var queryBacklogs = query.Query(String.Format(wiqlBacklogs, String.Join(",", relatedLinkIds.ToArray())));

            var backlogList = queryBacklogs.Cast<WorkItem>().ToList();
            foreach (var productBacklog in backlogList)
            {
                foreach (
                    var linkId in
                        productBacklog.Links.OfType<RelatedLink>().Select(relatedLink => relatedLink.RelatedWorkItemId))
                {
                    if (!featureProductBacklogMapping.ContainsKey(linkId))
                    {
                        featureProductBacklogMapping.Add(linkId, new List<WorkItem>());
                    }
                    featureProductBacklogMapping[linkId].Add(productBacklog);
                }
            }
            return featureProductBacklogMapping;
        }

        private Dictionary<int, List<WorkItem>> getProductBacklogTaskMapping(WorkItemStore query)
        {
            var productBacklogTaskMapping = new Dictionary<int, List<WorkItem>>();

            var wiqlTasks =
                "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.WorkItemType] = '工作' AND [System.State] = '完成' AND [Microsoft.VSTS.Common.StateChangeDate] >= @today - 30";
            var queryTasks = query.Query(String.Format(wiqlTasks, howManyDays));

            var taskList = queryTasks.Cast<WorkItem>().ToList();
            convertTaskListIntoBacklogTaskMapping(taskList, productBacklogTaskMapping);

            return productBacklogTaskMapping;
        }

        private static void convertTaskListIntoBacklogTaskMapping(List<WorkItem> taskList, Dictionary<int, List<WorkItem>> productBacklogTaskMapping)
        {
            foreach (var task in taskList)
            {
                convertRelatedBacklogIntoBacklogTaskMapping(productBacklogTaskMapping, task);
            }
        }

        private static void convertRelatedBacklogIntoBacklogTaskMapping(Dictionary<int, List<WorkItem>> productBacklogTaskMapping, WorkItem task)
        {
            foreach (var featureId in task.Links.OfType<RelatedLink>().Select(relatedLink => relatedLink.RelatedWorkItemId))
            {
                if (!productBacklogTaskMapping.ContainsKey(featureId))
                {
                    productBacklogTaskMapping.Add(featureId, new List<WorkItem>());
                }
                productBacklogTaskMapping[featureId].Add(task);
            }
        }

        public class MyWorks
        {
            public string TopCategory { get; set; }

            public List<FeatureTaskList> FeatureTaskList { get; set; }
        }

        public class FeatureTaskList
        {
            public string FeatureName;
            public List<string> TaskList;
        }
    }
}