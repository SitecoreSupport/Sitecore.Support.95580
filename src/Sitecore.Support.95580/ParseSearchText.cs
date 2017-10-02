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

namespace Sitecore.Support
{
    public class ParseSearchText : DefinitionBasedSearchProcessor
    {
        public static readonly Sitecore.Data.ID AllItemId = new Sitecore.Data.ID("{56A04961-F8A0-45BC-A870-D7371FD09F47}");

        public static readonly Sitecore.Data.ID SearchId = new Sitecore.Data.ID("{648CE334-864D-4373-A632-CD0DCA4E00B9}");

        private static readonly string[] AndSplitter = new string[]
        {
            " AND "
        };

        private static readonly string[] OrSplitter = new string[]
        {
            " OR "
        };

        public override void Process(SearchArgs args)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(args, "args");
            args.Queryable = this.Parse(args.ProviderSearchContext, args.SearchText);
        }

        private string MakeWildcard(string text)
        {
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }
            return "*" + text.Trim() + "*";
        }

        private Expression<Func<ConvertedSearchResultItem, bool>> MakeFromToExpression(string name, string value)
        {
            int num = value.IndexOf(" TO ");
            string start = value.Mid(1, num - 1);
            string end = value.Mid(num + 4);
            end = end.Left(end.Length - 1);
            Expression<Func<ConvertedSearchResultItem, bool>> first = (ConvertedSearchResultItem i) => i[name].CompareTo(start) >= 0;
            Expression<Func<ConvertedSearchResultItem, bool>> second = (ConvertedSearchResultItem i) => i[name].CompareTo(end) <= 0;
            return first.And(second);
        }

        private IQueryable<ConvertedSearchResultItem> Parse(IProviderSearchContext providerSearchContext, string searchText)
        {
            string text = string.Empty;
            Expression<Func<ConvertedSearchResultItem, bool>> expression = null;
            string[] array = searchText.Split(ParseSearchText.AndSplitter, StringSplitOptions.RemoveEmptyEntries);
            string[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                string text2 = array2[i];
                string text3 = text2;
                if (text3.StartsWith("("))
                {
                    text3 = text3.Mid(1);
                    text3 = text3.Left(text3.Length - 1);
                    Expression<Func<ConvertedSearchResultItem, bool>> expression2 = null;
                    string[] array3 = text3.Split(ParseSearchText.OrSplitter, StringSplitOptions.RemoveEmptyEntries);
                    string[] array4 = array3;
                    for (int j = 0; j < array4.Length; j++)
                    {
                        string text4 = array4[j];
                        int num = text4.IndexOf(':');
                        string name = text4.Left(num);
                        string value = text4.Mid(num + 1);
                        if (value.StartsWith("[") && value.Contains(" TO "))
                        {
                            Expression<Func<ConvertedSearchResultItem, bool>> expression3 = this.MakeFromToExpression(name, value);
                            expression2 = ((expression2 == null) ? expression3 : expression2.Or(expression3));
                        }
                        else
                        {
                            Expression<Func<ConvertedSearchResultItem, bool>> expression4 = (ConvertedSearchResultItem count) => count[name] == value;
                            expression2 = ((expression2 == null) ? expression4 : expression2.Or(expression4));
                        }
                    }
                    expression = ((expression == null) ? expression2 : expression.And(expression2));
                }
                else
                {
                    text = text + " " + text3;
                }
            }
            IQueryable<ConvertedSearchResultItem> query = this.GetQuery(providerSearchContext, text);
            if (expression != null)
            {
                return query.Where(expression);
            }
            return query;
        }

        private IQueryable<ConvertedSearchResultItem> GetQuery(IProviderSearchContext providerSearchContext, string queryText)
        {
            QueryUtil queryUtil = ApiFactory.Instance.GetQueryUtil();
            if (string.IsNullOrEmpty(queryText))
            {
                return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, string.Empty);
            }
            string text = queryText.Trim();
            List<SearchStringModel> searchStringModel = new List<SearchStringModel>
            {
                new SearchStringModel
                {
                    Operation = "should",
                    Type = "text",
                    Value = this.MakeWildcard(text)
                }
            };
            return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, searchStringModel);
        }
    }
}
