## Summary

The `list-remote-branches` command help you find the branches to clone of a TFS server.

## Synopsis

	Usage: git tfs list-remote-branches tfs-url-or-instance-name
	       git tfs list-remote-branches http://myTfsServer:8080/tfs/TfsRepository

	  -h, -H, --help
	  -V, --version
	  -d, --debug                Show debug output about everything git-tfs does
	  -u, --username=VALUE       TFS username
	  -p, --password=VALUE       TFS password

## Prerequisite

To be able to return the list of the existing branches, all your source code folders corresponding to branches
should be converted into branches (a notion introduced by TFS2010).

If none of your branches is converted into branch in your TFS server, the command will return the message :

    No TFS branches were found!

To change that, you should :

- Open 'Source Control Explorer'
- For each folder corresponding to a branch, right click on your source folder and select 'Branching and Merging' > 'Convert to branch'.

## Examples

To display all the remotes of a TFS server, do this:

    git tfs list-remote-branches http://tfs:8080/tfs/DefaultCollection

## Output
	TFS branches that could be cloned :

	 $/project/trunk [*]
	 |
	 +- $/project/branch1
	 |
	 +- $/project/branch2
	 |
	 +- $/project/branch3
		|
		+- $/project/branch3-1
		
	Cloning root branches (marked by [*]) is recommended!

Then you can use the [`clone`](clone.md) command with the good remote branch of your choice!
However, it is recommended to clone the root branch and then use the [`branch`](branch.md) command to manage branches.

### Authentication

If the TFS server need an authentication, you could use the _--username_ and _--password_ parameters. If you don't specify theses informations, you will be prompted to enter them. This informations are not store by git-tfs.

    git tfs list-remote-branches http://tfs:8080/tfs/DefaultCollection -u=DISSRVTFS03\peter.pan -p=wendy

## See also

* [clone](clone.md)
* [quick-clone](quick-clone.md)
* [init-branch](init-branch.md)
