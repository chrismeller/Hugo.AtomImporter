using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;

namespace Hugo.AtomImporter.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Any(x => x.Contains("--input")) == false ||
                args.Any(x => x.Contains("--output")) == false)
            {
                Console.WriteLine("Usage: dotnet run Hugo.AtomImporter.Client --input=./some/feed.xml --output=./output/directory [--categories-as-tags]");
                Environment.Exit(-1);
            }

            var input = args.First(x => x.Contains("--input")).Split('=').Last();
            var output = args.First(x => x.Contains("--output")).Split('=').Last();
            var categoriesAsTags = args.Any(x => x.Contains("--categories-as-tags"));

            Console.WriteLine("Input: " + input);
            Console.WriteLine("Output: " + output);
            Console.WriteLine("Categories as Tags: " + ((categoriesAsTags) ? "true" : "false"));

            var feedContent = File.ReadAllText(input);
            var items = AtomFeedParser.Parse(feedContent);

            var hugoPosts = items.Select(x =>
            {
                var post = new HugoPost()
                {
                    Content = x.Content.Body,
                    ContentType = x.Content.ContentType,
                    FrontMatter = new HugoFrontMatter()
                    {
                        Title = x.Title,
                        Date = x.Published,
                        Description = x.Summary,
                    },
                    
                };

                if (categoriesAsTags)
                {
                    post.FrontMatter.Tags = x.Categories;
                }
                else
                {
                    post.FrontMatter.Categories = x.Categories;
                }

                // parse out the last segment of the Uri to use as the slug
                var slug = x.Link.AbsolutePath.Split('/').Last();

                post.FrontMatter.Slug = slug;

                return post;
            });

            foreach(var post in hugoPosts)
            {
                var frontMatter = "";
                frontMatter += "+++\n";
                frontMatter += $"date = \"{post.FrontMatter.Date:O}\"\n";
                frontMatter += $"slug = \"{post.FrontMatter.Slug}\"\n";
                frontMatter += $"title = \"{post.FrontMatter.Title.Replace("\"", "\\\"")}\"\n";

                if (String.IsNullOrWhiteSpace(post.FrontMatter.Description) == false)
                {
                    frontMatter += $"description = \"{post.FrontMatter.Description}\"\n";
                }

                if (post.FrontMatter.Categories != null && post.FrontMatter.Categories.Any())
                {
                    frontMatter += $"categories = [ \"" + String.Join($"\", \"", post.FrontMatter.Categories) + $"\" ]\n";
                }

                if (post.FrontMatter.Tags != null && post.FrontMatter.Tags.Any())
                {
                    frontMatter += $"tags = [ \"" + String.Join($"\", \"", post.FrontMatter.Tags) + $"\" ]\n";
                }

                frontMatter += "+++\n";

                // and finally add the post content itself
                frontMatter += post.Content;

                var postFilename = post.FrontMatter.Slug + "." + post.ContentType;
                File.WriteAllText(Path.Join(output, postFilename), frontMatter);

                Console.WriteLine("Wrote post " + postFilename);
            }

        }
    }

    public class AtomFeed
    {
        public string Title { get; set; }
        public Uri Link { get; set; }
        public Uri LinkSelf { get; set; }
        public DateTimeOffset? Updated { get; set; }

        public IEnumerable<AtomFeedItem> Entries { get; set; }
    }

    public class AtomFeedItem
    {
        public string Title { get; set; }
        public Uri Link { get; set; }
        public string Id { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public AtomContent Content { get; set; }
        public AtomAuthor Author { get; set; }
        public string Summary { get; set; }
        public IEnumerable<string> Categories { get; set; }
    }

    public class AtomAuthor
    {
        public string Name { get; set; }
        public Uri? Uri { get; set; }
    }

    public class AtomContent
    {
        public string Body { get; set; }
        public string ContentType { get; set; }
    }

    public class HugoFrontMatter
    {
        public IEnumerable<string> Categories { get; set; }
        public DateTimeOffset? Date { get; set; }
        public string Description { get; set; }
        public string Slug { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string Title { get; set; }
    }

    public class HugoPost
    {
        public string Content { get; set; }
        public string ContentType { get; set; }
        public HugoFrontMatter FrontMatter { get; set; }
    }

    public class AtomFeedParser
    {
        public static IEnumerable<AtomFeedItem> Parse(string feedContent)
        {
            // load the feed contents into an xml document
            var doc = new XmlDocument();
            doc.LoadXml(feedContent);

            if (doc == null) throw new Exception("Failed to load input feed.");

            // because atom documents are properly namespaced XML, we need to handle the namespace when querying
            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("a", "http://www.w3.org/2005/Atom");

            // start our list of feed items
            var items = new List<AtomFeedItem>();

            // find all of our entries and iterate over them
            var entries = doc.SelectNodes("/a:feed/a:entry", namespaceManager);
            foreach (XmlNode entry in entries)
            {
                // pull out all the elements we care about
                var id = entry.SelectSingleNode("./a:id/text()", namespaceManager)?.Value;
                var authorName = entry.SelectSingleNode("./a:author/a:name/text()", namespaceManager)?.Value;
                var authorUri = entry.SelectSingleNode("./a:author/a:uri/text()", namespaceManager)?.Value;
                var updated = entry.SelectSingleNode("./a:updated/text()", namespaceManager)?.Value;
                var published = entry.SelectSingleNode("./a:published/text()", namespaceManager)?.Value;
                var title = entry.SelectSingleNode("./a:title/text()", namespaceManager)?.Value;
                var content = entry.SelectSingleNode("./a:content/text()", namespaceManager)?.Value;
                var contentType = entry.SelectSingleNode("./a:content/@type", namespaceManager)?.Value;
                var summary = entry.SelectSingleNode("./a:summary/text()", namespaceManager)?.Value;

                // we need to make sure the link we get is the alternate link, rather than the edit link you can also supply with APP
                var link = entry.SelectSingleNode("./a:link[ @rel='alternate' ]/@href", namespaceManager)?.Value;

                var categories = entry.SelectNodes("./a:category/@term", namespaceManager);
                var categoryValues = new List<string>();
                foreach(XmlNode category in categories)
                {
                    categoryValues.Add(category.Value);
                }

                var item = new AtomFeedItem()
                {
                    Title = title,
                    Author = new AtomAuthor()
                    {
                        Name = authorName,
                        Uri = (authorUri == null) ? null : new Uri(authorUri),
                    },
                    Categories = categoryValues,
                    Content = new AtomContent()
                    {
                        Body = content,
                        ContentType = contentType,
                    },
                    Id = id,
                    Link = new Uri(link),
                    Published = DateTimeOffset.Parse(published),
                    Summary = summary,
                    Updated = (updated == null) ? DateTimeOffset.Parse(published) : DateTimeOffset.Parse(updated),
                };

                items.Add(item);
            }

            return items;
        }
    }
}
