# Contributing

When contributing to this repository, please first discuss the change you wish to make via issue,
email, or any other method with the owners of this repository before making a change. 

Please note we have a code of conduct. Please follow it in all your interactions with the project.

## Rules
1. [KISS](https://en.wikipedia.org/wiki/KISS_principle) is the primary principle at ZoneTree. Please make your changes without violating the KISS principle.
2. ZoneTree is a low-level database engine. Thus it is good to make micro-optimization for better speed and less memory usage. For example, use *for* loop instead of a *foreach* iteration.
3. Add unit tests that cover the functionality of your changes including edge cases.
4. By contributing ZoneTree, you agree that your changes are included in ZoneTree with the [MIT license](https://github.com/koculu/ZoneTree/blob/main/LICENSE). This license agreement cannot be undone.
5. Make sure you are not violating some other 3rd party licenses with your contribution. For example, don't copy source code from other open source or commercial software without adding the necessary LICENSE copyright notice!
6. ZoneTree does not reference any 3rd party library except compression libraries. If you are going to add a dependency to a 3rd party library, there should be a good reason for it. Discuss with the maintainers if you need to add a 3rd party reference.
7. When you do a performance optimization make sure that you measure the difference with real benchmark code included in your pull request.
8. Rules are subject to change. Please add your complaints and suggestions to the discussion page.
