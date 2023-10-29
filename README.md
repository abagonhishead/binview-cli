# binview-cli
A quick and dirty command-line tool for generating images from binary data.

I mostly wrote this for fun -- sometimes looking at memory dumps or binary data as an image can tell you a lot about what it contains and how the data is structured. It's also part of a wider project I'm working towards that relates to image [steganography](https://en.wikipedia.org/wiki/Steganography) and storing binary data in image format.

# Usage
Build it, and then:

`binview-cli --input-path '/path/to/some/binary/file' --output-path '/path/to/save/the/image.png'`

For a full list of options:
`binview-cli --help`

````
binview-cli: a small command-line application for generating images from binary files.

Usage: binview-cli --input-path {file-path} --output-path {file-path} [--max-concurrency {max-thread-count}] [--log-level {level}] [--log-show-timestamp] [--help] [--version]

All command-line arguments should be prefixed with two dashes ('--')
Command-line arguments:

    input-path {file-path}                 Required. String. The path to the file to read from.
    output-path {file-path}                Required. String. The path to write the output file to.
    max-concurrency {max-thread-count}     Optional. Integer. The maximum number of worker threads to run concurrently. Defaults to the logical processor count plus 1.
    log-level {level}                      Optional. Set the log level. Valid values are: 'Trace'/'0', 'Debug'/'1', 'Information'/'2', 'Warning'/'3', 'Error'/'4', 'Critical'/'5', 'None'/'6'
    log-show-timestamp                     Optional. Switch. Show timestamps for log output
    help                                   Optional. Switch. Print the help output (this)
    version                                Optional. Switch. Print the current version and exit.
````

# Contributing?
Feel free to contribute. 

Also feel free to fork and improve, but I'd appreciate a message so I can see what you've done.

# TODO
- Unit tests
- Check *nix support. It should be cross-platform but I haven't tested it yet.
- Build automation & binary release
