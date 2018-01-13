# Kazedan
This software has been renamed to _Kazedan_.

### What is it?
A program that visualizes a MIDI file on a piano, Synthesia style.

### [Normal Demonstration](https://youtu.be/6k5ZzvepsmU)
### [Instrument Demonstration](https://youtu.be/Iy8vpjXQ7Oc)
### [Stress Test Demonstration](https://www.youtube.com/watch?v=-ewiDnA43w8)
[Demonstration #1](https://www.youtube.com/watch?v=ZX1CaQDmyOo)<br>
[Demonstration #2](https://www.youtube.com/watch?v=W6EMQiqftfM)<br>
[Demonstration #3](https://youtu.be/9EoHmNg9MWs)

![alt text](http://i.imgur.com/hcP8WON.png)

### Libraries Used
[Sanford.Multimedia.Midi](https://github.com/tebjan/Sanford.Multimedia.Midi)<br>
[SlimDX](https://slimdx.org/)

You may just use Nuget to restore these packages when building.

### Build Instructions
Use MSBuild (or xbuild if you're a linux user) to build the Release target, and find the binary Kazedan.exe in bin/Release.

### Older Demonstrations (using various MIDI files)
**version 0.0.1.0 (GDI+ rendering engine)**<br>
[[1]](https://streamable.com/2ta5)
[[2]](https://streamable.com/gu1p)

**version 0.1.0.0 (Direct2D rendering engine)**<br>
[[3]](https://streamable.com/kc2z)
[[4]](https://streamable.com/m7pb)
[[5]](https://streamable.com/mj1e)
[[6]](https://streamable.com/d19m)

**version 0.8.0.0 (Redesigned rendering engine)**<br>
[[7]](https://www.youtube.com/watch?v=lCzUmw7Az2k)
[[8]](https://www.youtube.com/watch?v=_VbeNVNvHyI)


### Todo
Despite how optimized the note drawing and timing procedures are already, there is still room for optimization:
- Improving caching of note structures
- Improving storage of note structures
- Improving callback of note events
- Caching graphical objects and reducing instantiation
- Perhaps actually learning Direct3D and using that instead
