namespace Sitecore.Support.ItemWebApi.Pipelines.Search
{
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Linq.Utilities;
    using Sitecore.ContentSearch.Utilities;
    using Sitecore.Data;
    using Sitecore.Diagnostics;
    using Sitecore.Extensions.StringExtensions;
    using Sitecore.ItemWebApi;
    using Sitecore.ItemWebApi.Pipelines.Search;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    public class ParseSearchText : DefinitionBasedSearchProcessor
    {
        public static readonly ID AllItemId = new ID("{56A04961-F8A0-45BC-A870-D7371FD09F47}");
        private static readonly string[] AndSplitter = new string[] { " AND " };
        private static readonly string[] OrSplitter = new string[] { " OR " };
        public static readonly ID SearchId = new ID("{648CE334-864D-4373-A632-CD0DCA4E00B9}");

        private IQueryable<ConvertedSearchResultItem> GetQuery(IProviderSearchContext providerSearchContext, string queryText)
        {
            QueryUtil queryUtil = ApiFactory.Instance.GetQueryUtil();
            if (string.IsNullOrEmpty(queryText))
            {
                return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, string.Empty);
            }
            string text = queryText.Trim();
            List<SearchStringModel> list2 = new List<SearchStringModel>();
            SearchStringModel item = new SearchStringModel
            {
                Operation = "should",
                Type = "text",
                Value = this.MakeWildcard(text)
            };
            list2.Add(item);
            List<SearchStringModel> searchStringModel = list2;
            return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, searchStringModel);
        }

        private Expression<Func<ConvertedSearchResultItem, bool>> MakeFromToExpression(string name, string value)
        {
            int index = value.IndexOf(" TO ");
            string start = value.Mid(1, index - 1);
            string end = value.Mid(index + 4);
            end = end.Left(end.Length - 1);
            Expression<Func<ConvertedSearchResultItem, bool>> first = i => i[name].CompareTo(start) >= 0;
            Expression<Func<ConvertedSearchResultItem, bool>> second = i => i[name].CompareTo(end) <= 0;
            return first.And<ConvertedSearchResultItem>(second);
        }

        private string MakeWildcard(string text)
        {
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }
            return text.Trim(); // Sitecore.Support.95580
        }

        private IQueryable<ConvertedSearchResultItem> Parse(IProviderSearchContext providerSearchContext, string searchText)
        {
            string queryText = string.Empty;
            Expression<Func<ConvertedSearchResultItem, bool>> first = null;
            foreach (string str2 in searchText.Split(AndSplitter, StringSplitOptions.RemoveEmptyEntries))
            {
                string text = str2;
                if (text.StartsWith("("))
                {
                    text = text.Mid(1);
                    text = text.Left(text.Length - 1);
                    Expression<Func<ConvertedSearchResultItem, bool>> expression2 = null;
                    foreach (string str4 in text.Split(OrSplitter, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int index = str4.IndexOf(':');
                        string name = str4.Left(index);
                        string value = str4.Mid(index + 1);
                        if (value.StartsWith("[") && value.Contains(" TO "))
                        {
                            Expression<Func<ConvertedSearchResultItem, bool>> second = this.MakeFromToExpression(name, value);
                            expression2 = (expression2 == null) ? second : expression2.Or<ConvertedSearchResultItem>(second);
                        }
                        else
                        {
                            Expression<Func<ConvertedSearchResultItem, bool>> expression4 = i => i[name] == value;
                            expression2 = (expression2 == null) ? expression4 : expression2.Or<ConvertedSearchResultItem>(expression4);
                        }
                    }
                    first = (first == null) ? expression2 : first.And<ConvertedSearchResultItem>(expression2);
                }
                else
                {
                    queryText = queryText + " " + text;
                }
            }
            IQueryable<ConvertedSearchResultItem> query = this.GetQuery(providerSearchContext, queryText);
            if (first != null)
            {
                return query.Where<ConvertedSearchResultItem>(first);
            }
            return query;
        }

        public override void Process(SearchArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            args.Queryable = this.Parse(args.ProviderSearchContext, args.SearchText);
        }
    }
}
