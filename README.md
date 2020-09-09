A simple C# .Net Core console app that takes all the entries from a properly-formatted Atom feed and spits them out into the TOML format for use with a Hugo static site.

Usage:
	Show arguments: dotnet run --project /path/to/Hugo.AtomImporter/Hugo.AtomImporter.Client
	Convert: dotnet run --project /path/to/Hugo.AtomImporter/Hugo.AtomImporter.Client --input=/some/atom/feed.xml --output=/your/hugo/content/posts/ [--categories-as-tags]

Note the optional --categories-as-tags arg. If you leave it out, all the category elements from your feed will be added as categories in your Hugo post. If you include the arg, it will instead add them as tags.
