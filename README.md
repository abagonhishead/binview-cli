# binview-cli
A quick and dirty command-line tool for generating images from binary data

# Usage
Build it, and then:
`binview-cli --input-path '/path/to/some/binary/file' --output-path '/path/to/save/the/image.png'`

# TODO
- Improve performance by parallelising large images
- Remove dependency on System.Drawing.Common since *nix support is deprecated on .NET 7
