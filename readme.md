# Plugin Template

## Overview

This is a template for creating plugins for Launchbox. It's designed to be as "out of the box" as possible, but there are a few things you need to do before compilation.

This template stores any libraries that are shared with Launchbox (in the Core folder) in the directory `{PluginSolutionFolder}/lib/launchbox`. You need to have these here for reference, but they don't compile to the eventual plugin runtime as these are accessed form the Launchbox instance that the plugin is running from.

It should run without modification, but it is worth updating these to the latest versions form your Launchbox installation. DO NOT install these form a Beta version. Only from Release versions (you can temp install the Launchbox below beta version to get the relevant libs if you need to).

## Installing the LaunchBox Plugin Template

1. Download the Template
Download the latest RommStar.zip from the releases page or your distribution source.

2. Install the Template
Open a terminal (PowerShell or Command Prompt) and run:
`dotnet new install path\to\RommStar.zip`
Replace path\to\RommStar.zip with the actual path to the downloaded zip file.

3. Create a New Project from the Template
After installing, open Visual Studio and go to `File > New > Project`. Search for LaunchBox Plugin in the project templates.
•    Make sure to check "Place solution and project in the same directory" during creation.

4. Uninstall the Template (Optional)
If you ever want to remove the template, run:
`dotnet new uninstall path\to\RommStar.zip`

## Getting Started

Create a new project form this template. Make sure you select "Place solution and project in the same directory" when creating the project. This will ensure that the plugin is created in the correct location for Launchbox to find it.

You will also be asked for your Launchbox installation's Plugin folder (I'd recommend a dev installation rather than working with your live one). This ties everything together behind the scenes.

That should be it. The debug paths and copying the plugin to your plugin folder should now work from the start. If you build it, you should see the plugin "LaunchBox Plugin" under your Tools menu. You can also run it in debug mode by selecting "PluginDebug" from the drop-down in the toolbar and hitting F5. This will launch a new instance of Launchbox with the plugin loaded and attached to the debugger.

Now over to you. Go create!