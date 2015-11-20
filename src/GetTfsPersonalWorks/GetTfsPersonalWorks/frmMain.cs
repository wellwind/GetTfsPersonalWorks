using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Discussion.Client;
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

            var queryTasks = query.Query("SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.WorkItemType] = '工作' AND [System.State] = '完成' AND [Microsoft.VSTS.Common.StateChangeDate] >= @today - 30");
            Dictionary<int, List<WorkItem>> featureTasksMapping = new Dictionary<int, List<WorkItem>>();
            var relatedLinkIds = new List<int>();
            List<WorkItem> taskList = queryTasks.Cast<WorkItem>().ToList();
            foreach (var task in taskList)
            {
                foreach (Link relatedFeature in task.Links)
                {
                    if (relatedFeature is RelatedLink)
                    {
                        var featureId = (relatedFeature as RelatedLink).RelatedWorkItemId;
                        relatedLinkIds.Add(featureId);
                        if (!featureTasksMapping.ContainsKey(featureId))
                        {
                            featureTasksMapping.Add(featureId, new List<WorkItem>());
                        }
                        featureTasksMapping[featureId].Add(task);
                    }
                }
            }

            var wiqlFeatures = "SELECT [System.Title], [System.Tags] FROM WorkItems WHERE [System.Id] IN ({0}) AND [System.WorkItemType] = '產品待處理項目'";
            var queryFeatures = query.Query(String.Format(wiqlFeatures, String.Join(",", relatedLinkIds.ToArray())));
            foreach (WorkItem feature in queryFeatures)
            {
                var tag = feature.Fields["System.Tags"].Value == null ? "" : feature.Fields["System.Tags"].Value.ToString();
                if (!workList.ContainsKey(tag))
                {
                    workList.Add(tag, new MyWorks() { TopCategory = tag, FeatureTaskList = new List<FeatureTaskList>() });
                }

                workList[tag].FeatureTaskList.Add(new FeatureTaskList()
                {
                    FeatureName = feature.Fields["System.Title"].ToString(),
                    TaskList = featureTasksMapping[feature.Id].OrderBy(task => task.Id).Select(task => task.Title).ToList()
                });
            }

            // TODO: output to markdown
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