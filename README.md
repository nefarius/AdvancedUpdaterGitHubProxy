# <img src="assets/NSS-128x128.png" align="left" />AdvancedUpdaterGitHubProxy

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/nefarius/AdvancedUpdaterGitHubProxy/docker-image.yml)

Builds an [Advanced Installer Updater](https://www.advancedinstaller.com/user-guide/updater.html) Configuration File from GitHub Releases.

## Why? The Advanced Installer Updater already has [GitHub Integration](https://www.advancedinstaller.com/user-guide/qa-github-updater-integration.html#qa-github-updater-integration)?

Although GitHub Integration now being a thing, I like the freedom of total customization with this proxy approach. Reasons include:

- **Custom domain and load-balancing**  
When hard-coding the GitHub releases URL you'll (obviously) depend on GitHubs availability and rate limits. With this proxy you can completely customize the updater URL including the domain and using custom DNS load balancing and fault tolerance settings.
- **Extra fields are read from Markdown body as HTML comments**  
Instead of having to attach an `advinst_update.json` asset on each release the custom fields (`RegistryKey`, `Flags`, etc.) can be embedded in a specially formatted HTML comment block in the Markdown body of the release. IMHO having one less visible asset that could only cause user confusion is a benefit 😁
- **Caching**  
Request responses to the update configuration file URL get cached in memory for an hour. This reduces the load on the GitHub API and eliminates the risk of getting block temporarily for exceeding the public API rate limits.
- **Logging and statistics**  
Why should only Big Tech have all your data? 🤣 In all seriousness though, being able to generate statistics from access data while not being completely dependant on GitHub sure feels nice.
- **Easy to disable updates if something goes wrong**  
When something goes wrong (bug in an update) you can simply pull the service without having to touch the release properties.
- **Simplifies migration**  
Should the need arise to migrate releases away from GitHub you can do so without having to publish updates with a new URL, simply equip the proxy service with new backend logic and users won't notice a thing!

## How to use

It's recommended to build a Docker Container with the provided Dockerfile and spin it up with the example compose file. Should run fine on any cloud provider (or VM, bare metal) offering Docker support.

## How to build

```bash
docker build --platform linux/amd64 -t nefarius.azurecr.io/aughp:dev .
docker push nefarius.azurecr.io/aughp:dev
```

## Enriched release Markdown example

Put a comment block like below at the top of every GitHub release Markdown body, those will get parsed and merged automatically into the API response when requested:

```md
<!--
{
  "available": true,
  "registryKey": "HKUD\\Software\\Nefarius Software Solutions e.U.\\ViGEm Bus Driver\\Version",
  "flags": "NoRedetect"
}
-->
```

❗ The content of the comment block must be valid JSON in camelCase.  
❗ If you set `available` to `false` it will be skipped/excluded, even if it's the currently marked latest release.

## Get JSON response

If you'd like to get the latest cached release as original JSON (same schema as GitHub's API response) simply add the `?asJson=true` query parameter.

### Get latest JSON whether it has an enriched body or not

You can add the `?asJson=true&allowAny=true` query parameters to get the latest GitHub release, even if it doesn't contain the embedded updater meta-data in the body content. 

## W3C log analysis

Two types of logs get produced in the `./logs` subdirectory during normal operation; a `server-*.log` which logs all normal application events and errors and gets cycled daily and `access-*.txt` access logs in W3C format. The latter can be visualized with [GoAccess](https://goaccess.io/) using the following custom log filter:

```bash
goaccess -c logs/access-20221017.0002.txt --date-format='%Y-%m-%d' --time-format='%H:%M:%S' --log-format='%d %t %h %^ %^ %^ %^ %m %U %^ %s %L %^ %v %u %^ %^'
```

For more options like HTML report generation [consult their FAQ](https://goaccess.io/faq).

## Configure beta clients

You can configure delivering pre-releases to selected test clients by doing two things:

- Mark the release on GitHub as a pre-release and add the JSON snippet outlined above
- Add one or more public IP addresses of your test clients' internet breakout to `appsettings.json` like so:  
  ```json
  {
    "UpdatesEndpoint": {
      "BetaClients": [
        "245.36.203.69",
        "117.132.178.250",
        "116.96.141.250"
      ]
    }
  }
  ```   

If web requests come in from these configured addresses, the cache is bypassed and the latest pre-release will be selected for delivery. You can now test your update with a small group of beta clients without influencing anything for the majority of your users!

## Blacklist usernames or repositories

You can deny delivering updater configurations to entire user accounts or individual repositories like so:

```json
{
    "UpdatesEndpoint": {
        "BlacklistedUsernames": [
            "CircumSpector"
        ],
        "BlacklistedRepositories": [
            "ViGEmBus",
            "HidHide"
        ]
    }
}
```

This will deliver 404s for all requests targeting the matching routes. 

## 3rd party credits

- [FastEndpoints](https://github.com/FastEndpoints/Library)
- [Polly](https://github.com/App-vNext/Polly)
- [prometheus-net](https://github.com/prometheus-net/prometheus-net)
