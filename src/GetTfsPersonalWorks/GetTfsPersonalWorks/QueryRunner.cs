using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GetTfsPersonalWorks
{
    internal class QueryRunner
    {
        public WorkItemStore WorkItemStore { get; private set; }
        public string TeamProjectName { get; private set; }
        public string CurrentUserDisplayName { get; private set; }

        public QueryRunner(string tpcUrl, string teamProjectName)
        {
            var tpc = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tpcUrl));
            WorkItemStore = tpc.GetService<WorkItemStore>();
            TeamProjectName = teamProjectName;
        }

        public List<WorkItemNode> RunQuery(Guid queryGuid)
        {
            // get the query
            var queryDef = WorkItemStore.GetQueryDefinition(queryGuid);
            var query = new Query(WorkItemStore, queryDef.QueryText, GetParamsDictionary());

            // get the link types
            var linkTypes = new List<WorkItemLinkType>(WorkItemStore.WorkItemLinkTypes);

            // run the query
            var list = new List<WorkItemNode>();
            if (queryDef.QueryType == QueryType.List)
            {
                foreach (WorkItem wi in query.RunQuery())
                {
                    list.Add(new WorkItemNode() { WorkItem = wi, RelationshipToParent = "" });
                }
            }
            else
            {
                var workItemLinks = query.RunLinkQuery().ToList();
                list = WalkLinks(workItemLinks, linkTypes, null);
            }

            return list;
        }

        public List<WorkItemNode> RunQuery(string wiql)
        {
            // run the query
            var list = new List<WorkItemNode>();
            foreach (WorkItem wi in WorkItemStore.Query(wiql))
            {
                list.Add(new WorkItemNode() { WorkItem = wi, RelationshipToParent = "" });
            }
            return list;
        }

        private List<WorkItemNode> WalkLinks(List<WorkItemLinkInfo> workItemLinks, List<WorkItemLinkType> linkTypes, WorkItemNode current)
        {
            var currentId = 0;
            if (current != null)
            {
                currentId = current.WorkItem.Id;
            }

            var workItems = (from linkInfo in workItemLinks
                             where linkInfo.SourceId == currentId
                             select new WorkItemNode()
                             {
                                 WorkItem = WorkItemStore.GetWorkItem(linkInfo.TargetId),
                                 RelationshipToParent = linkInfo.LinkTypeId == 0 ? "Parent" :
                                    linkTypes.Single(lt => lt.ForwardEnd.Id == linkInfo.LinkTypeId).ForwardEnd.Name
                             }).ToList();
            workItems.ForEach(w => w.Children = WalkLinks(workItemLinks, linkTypes, w));
            return workItems;
        }

        private void ResolveCurrentUserDisplayName()
        {
            var securityService = WorkItemStore.TeamProjectCollection.GetService<IGroupSecurityService>();
            var accountName = string.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName);
            var memberInfo = securityService.ReadIdentity(SearchFactor.AccountName, accountName, QueryMembership.None);
            if (memberInfo != null)
            {
                CurrentUserDisplayName = memberInfo.DisplayName;
            }
            else
            {
                CurrentUserDisplayName = Environment.UserName;
            }
        }

        private IDictionary GetParamsDictionary()
        {
            return new Dictionary<string, string>()
            {
                { "project", TeamProjectName },
                { "me", CurrentUserDisplayName }
            };
        }
    }
}