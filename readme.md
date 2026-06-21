iiEighthSolitude
=========

iiEighthSolitude is a C# library supporting the modification of files relating to 7th Legion, the 1997 RTS game developed by Vision Software.

| Name   | Read | Write | Comment
|--------|:----:|-------|--------
| AI     | ✔   |   ✔   | Plain text
| AIO    | ✗   |   ✗   | Plain text
| BIM    | ✔   |   ✗   | Some images seem to contain invalid data
| BIN    | ✗   |   ✗   |  
| C24    | ✔   |   ✔   | Binary - palette (256 colours, 6-bit, 4 bytes/entry)
| COL    | ✔   |   ✔   | 
| DAT    | ✔   |   ✔   | Binary - palette
| DAT    | ✗   |   ✗   | Plain text
| GP     | ✗   |   ✗   | Plain text
| INI    | ✗   |   ✗   | Plain text
| SAV    | ✗   |   ✗   | Save game
| SMP    | ✔   |   ✔   | Sound effects
| TXT    | ✗   |   ✗   | Plain text
| WS1    | ✗   |   ✗   | 
| XFD    | ✔   |   ✔   | 
| XM     | ✔   |   ✗   | 

## Usage

Instantiate the relevant class and call the `Process` method passing the filename.

```csharp
var col = new ColProcessor();
var palette = col.Read(@"D:\Games\7thLegion\GFX\PAL1.COL");

var bim = new BimProcessor { Palette = palette };
foreach (var f in Directory.EnumerateFiles(@"D:\Games\7thLegion\GFX", "*.BIM"))
{
    var images = bim.Read(f);

    foreach (var image in images)
    {
        var n = Path.GetFileNameWithoutExtension(f);
        image.SaveAsPng($@"D:\data\7thLegion\GFX\{n}_" + images.IndexOf(image) + ".png");
    }
}
```


## Compiling

To clone and run this application, you'll need [Git](https://git-scm.com) and [.NET](https://dotnet.microsoft.com/) installed on your computer. From your command line:

```
# Clone this repository
$ git clone https://github.com/btigi/iiEighthSolitude

# Go into the repository
$ cd src

# Build  the app
$ dotnet build
```

## Licencing

iiEighthSolitude is licenced under the MIT License. Full licence details are available in licence.md

iiEighthSolitude includes [SharpMod](https://github.com/Mdwf-droid/SharpMod), licenced under MIT