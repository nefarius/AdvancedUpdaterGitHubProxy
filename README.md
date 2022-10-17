<img src="assets/NSS-128x128.png" align="right" />

# AdvancedUpdaterGitHubProxy

![GitHub Workflow Status](https://img.shields.io/github/workflow/status/nefarius/AdvancedUpdaterGitHubProxy/Docker%20Image%20CI)

Builds an [Advanced Installer Updater](https://www.advancedinstaller.com/user-guide/updater.html) Configuration File from GitHub Releases.

## Why? The Advanced Installer Updater already has [GitHub Integration](https://www.advancedinstaller.com/user-guide/qa-github-updater-integration.html#qa-github-updater-integration)?

Although GitHub Integration now being a thing, I like the freedom of total customization with this proxy approach. Reasons include:

- Custom domain and load-balancing  
When hard-coding the GitHub releases URL you'll (obviously) depend on GitHubs availability and rate limits. With this proxy you can completely customize the updater URL including the domain and using custom DNS load balancing and fault tolerance settings.
- Extra fields are read from Markdown body as HTML comments  
Instead of having to attach an `advinst_update.json` asset on each release the custom fields (`RegistryKey`, `Flags`, etc.) can be embedded in a specially formatted HTML comment block in the Markdown body of the release. IMHO having one less visible asset that could only cause user confusion is a benefit 😁
- Caching  
Request responses to the update configuration file URL get cached in memory for an hour. This reduces the load on the GitHub API and eliminates the risk of getting block temporarily for exceeding the public API rate limits.
- Logging and statistics  
Why should only Big Tech have all your data? 🤣 In all seriousness though, being able to generate statistics from access data while not being completely dependant on GitHub sure feels nice.
- Easy to disable updates if something goes wrong  
When something goes wrong (bug in an update) you can simply pull the service without having to touch the release properties.
- Simplifies migration  
Should the need arise to migrate releases away from GitHub you can do so without having to publish updates with a new URL, simply equip the proxy service with new backend logic and users won't notice a thing!

## How to use

It's recommended to build a Docker Container with the provided Dockerfile and spin it up with the example compose file. Should run fine on any cloud provider (or VM, bare metal) offering Docker support.

## 3rd party credits

- [FastEndpoints](https://github.com/FastEndpoints/Library)
- [Polly](https://github.com/App-vNext/Polly)
