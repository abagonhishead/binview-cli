# binview-cli
A quick and dirty command-line tool for generating images from binary data.

I mostly wrote this for fun -- sometimes looking at memory dumps or binary data as an image can tell you a lot about what it contains and how the data is structured. It's also part of a wider project I'm working towards that relates to image [steganography](https://en.wikipedia.org/wiki/Steganography) and storing binary data in image format.

# Usage
Build it, and then:

`binview-cli --input-path '/path/to/some/binary/file' --output-path '/path/to/save/the/image.png'`

# Contributing?
Feel free to contribute. 

Also feel free to fork and improve, but I'd appreciate a message so I can see what you've done.

# TODO
- Unit tests
- Check *nix support. It should be cross-platform but I haven't tested it yet.
- Build automation & binary release
