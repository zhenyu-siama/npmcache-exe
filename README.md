# npmcache-exe
npmcache tool for Windows NTFS file system.

## why npm cache?

In continuous integration, one principle is to detect any errors as quick as possible.

```npm install```

npm install one of the slowest part of the CI workflow. But it does nothing important but grabbing packages from npm.

In most of the CI builds, package.json does not change very often, and the whole node_modules folder is only determined by package.json file.

If we can cache the node_module folder for each package.json, that could potentially save us a lot of time in the CI.

## What's the idea? And what's up?

Symbolic link is the first idea that came out of my mind. I tried to cache the node_modules folder somewhere in the hard disk and create a symbolic link where the package.json should install modules to. This seems to be a good idea and it is super quick to create a symbolic link (less than 10ms vs the typical 60-120 sec with npm install). However, it works only for editing the code, when we use angular cli tools to serve or build, it fails.

Basically, angular cli tool must be located under the correct physical path so it can find the source codes and other files. When angular cli is in the symbolic link cache folder, it will find itself in the cached folder, not the linked position, and it is unable to find the source files for compilation. This made the story a bit complex.

## The workaround with hard link.

The good news is that Windows does not only support symbolic link, but also hard links. Hard links are only available for files, not folders. As a result, instead of copying all the file, creating real folder and hard links could be a lot faster.

With this idea, I found that I can "hard-link-copy" the whole npm folder in about 12-13 sec, which takes 75 sec with npm install. And because of the "real folders", the hard-linked files can find themselves at the correct physical location, and thus angular cli works for me now.

But 12 seconds is not fast enough. For something right there in the folder that we don't really want to "copy" at all, I hope it could be done in less than a second.

## The hybrid solution: symbolic links for most and hard links only for the necessary.

Since the majority of the node modules do not "RUN" at their physical positions, but are loaded by other "running" processes. So all those "non-running" modules can theoretically be linked with symbolic links. So I tried to maximize the usage of symbolic links and only apply real folder and hard links for tools that must run. I found that I am able to use symbolic link with most of the modules @angular/cli, applicationinsights-js, angular2-busy. As long as I apply hard links on them, the ng build would work.

The performance of this hybrid solution: 670 ms!

Yes, less than a second! That's the decent time "npm install" deserves.

## Further optimization?

I guess only the "entry point" files of the @angular/cli, applicationinsights-js, angular2-busy modules really need the hard links. But to acchieve that, the configuration would be super complex. That would not be really worth doing.

However, since we are doing "per module" cache now, it would be great to have a package manager that can cache modules based on the version number, instead of caching the whole node_modules folder.

In addition, hard links and symbolic links are available on linux as well.

## Summary:

We don't physically copy any files, we only create links, either symbolic or hard. Both the creation of links and deletion are flashing fast now. (You know how much time it takes to delete the node_modules folder. So the benefit you got is not just how fast to "copy" or "install").

## Setup
1. create a folder in your (most likely) C drive, and copy npmc tools into that folder. Add this folder to the PATH of your system environment variables.
2. your configuration file npmcache.json should look like this:
```javascript
{
  "CacheDirectory": "C:\\YourNPMCacheFolder",
  "PackageManager": "npm install", // this can be "yarn"
  "CheckInterval": 1, // this tells how frequently the npmc tool should check if there is a previous npmc tool is running "npm install" on the same package. the npmc tool can wait until the previous one is done.
  "Timeout": 600, // the max time you allow npmc tool to wait for previous "npm install".
  "HardLinkWhiteList": [ // this is the whitelist for using hard links. in our current project, I can identify the following ones as "running" modules.
    "@angular\\cli",
    "applicationinsights-js",
    "angular2-busy"
  ]
}
```
