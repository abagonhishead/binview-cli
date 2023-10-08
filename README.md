# binview-cli
A quick and dirty command-line tool for generating images from binary data.

When I say quick and dirty, I mean _really_ quick and dirty. I wrote this in 2 hours and just shared it because I thought other people might find it useful. It is slow when processing large files and probably won't run on Linux.

# Usage
Build it, and then:

`binview-cli --input-path '/path/to/some/binary/file' --output-path '/path/to/save/the/image.png'`

# TODO
- Improve performance by parallelising large images (probably one row per CPU or something)
- Remove dependency on System.Drawing.Common since *nix support is deprecated on .NET 7
