# Contributing to ZoneTree

Thank you for considering contributing to ZoneTree! To ensure a smooth collaboration, please take a moment to review the guidelines below.

## Before You Start

Before making any changes, please discuss the proposed modifications via an issue, email, or any other communication method with the repository owners. This helps ensure that your efforts align with the project's goals and avoids unnecessary work.

Please also familiarize yourself with our [Code of Conduct](CODE_OF_CONDUCT.md) and adhere to it in all interactions related to the project.

## Contribution Guidelines

1. **KISS Principle**: The [KISS](https://en.wikipedia.org/wiki/KISS_principle) (Keep It Simple, Stupid) principle is fundamental at ZoneTree. Ensure that your contributions are straightforward and do not add unnecessary complexity.

2. **Micro-Optimizations**: ZoneTree is a low-level database engine, and performance is critical. Micro-optimizations for speed and memory usage are encouraged. For example, prefer using a `for` loop over a `foreach` loop where performance gains can be realized.

3. **Unit Testing**: Every contribution should be accompanied by unit tests that thoroughly cover the new or modified functionality, including edge cases. This ensures that ZoneTree remains reliable and robust.

4. **License Agreement**: By contributing to ZoneTree, you agree that your changes will be included under the [MIT license](LICENSE). This agreement is permanent and cannot be revoked.

5. **Respect Third-Party Licenses**: Ensure that your contributions do not violate any third-party licenses. Do not copy code from other open-source or commercial software without including the necessary LICENSE and copyright notices.

6. **Minimizing Dependencies**: ZoneTree avoids dependencies on third-party libraries except for compression libraries. If your contribution requires adding a new dependency, it must be justified. Please discuss with the maintainers before proceeding.

7. **Performance Benchmarking**: When submitting performance optimizations, include benchmark code in your pull request to demonstrate the impact of your changes. This allows us to verify and appreciate the improvements.

8. **Open to Feedback**: The contribution rules are subject to change based on community feedback and project needs. If you have suggestions or concerns, please share them in the discussion page or via an issue.

Thank you for helping to make ZoneTree better!
