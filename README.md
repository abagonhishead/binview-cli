# binview-cli
A quick and dirty command-line tool for generating images from binary data.

When I say quick and dirty, I mean _really_ quick and dirty. I wrote this in 2 hours and just shared it because I thought other people might find it useful. It is slow when processing large files and probably won't run on Linux without moving it to SkiaSharp or something.

# Usage
Build it, and then:

`binview-cli --input-path '/path/to/some/binary/file' --output-path '/path/to/save/the/image.png'`

# Contributing?
Feel free to contribute. 

Also feel free to fork and improve, but I'd appreciate a message so I can see what you've done.

# TODO
- Improve performance by parallelising large images (probably one row per CPU or something)
- Remove dependency on System.Drawing.Common since *nix support is deprecated on .NET 7 (use SkiaSharp?)
- Check logging is actually working properly since I couldn't see any `LogLevel.Debug` events
