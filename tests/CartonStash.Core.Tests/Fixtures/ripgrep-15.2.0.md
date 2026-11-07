[Sponsorship is appreciated!](https://github.com/sponsors/BurntSushi/)

This release fixes a number of bugs related to gitignore matching as well as
some performance improvements in directory tree traversal.

> In case you haven't heard of it before, ripgrep is a line-oriented search
> tool that recursively searches the current directory for a regex pattern.
> By default, ripgrep will respect gitignore rules and automatically skip
> hidden files/directories and binary files.

Platform support:

* `aarch64-unknown-linux-musl` is now included in the release binaries for
  ripgrep.

Performance improvements:

* [PERF #3293](https://github.com/BurntSushi/ripgrep/issues/3293):
  Improves directory traversal time on very large corpora.

Feature enhancements:

* [FEATURE #3275](https://github.com/BurntSushi/ripgrep/issues/3275):
  ripgrep now respects `GIT_CONFIG_GLOBAL` and `GIT_CONFIG_SYSTEM`.

Bug fixes:

* [BUG #3212](https://github.com/BurntSushi/ripgrep/pull/3212):
  Don't check for the existence of `.jj` when `--no-ignore` is used.
* [BUG #3320](https://github.com/BurntSushi/ripgrep/pull/3419),
  [BUG #3376](https://github.com/BurntSushi/ripgrep/pull/3376),
  [BUG #3419](https://github.com/BurntSushi/ripgrep/pull/3419):
  Fix gitignore matching bug when searching across multiple directories.
