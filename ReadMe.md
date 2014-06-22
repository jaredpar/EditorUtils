### Editor Utilities Library

This is a utility library to be used with VSIX projects.  It abstracts away many of the problem areas of the Visual Studio API into simple to use types

AppVeyor: [![Build status](https://ci.appveyor.com/api/projects/status/vbgvb4ixgwbld943)](https://ci.appveyor.com/project/jaredpar/editorutils)

### Features

EditorUtils is a collection of abstractions for building VSIX projects.  It is broken down into the following feature areas 

#### Tagging

Syntax highlighting, brace completion, intra text adornments, etc ... are all features provided by the [ITagger<T>](http://msdn.microsoft.com/en-us/library/dd885020.aspx) interface.  The interface is simple enough but the rules and scenarios around this interface are complex and largely undocumented.  EditorUtils abstracts away much of this complexity by providing a much simpler interface IBasicTaggerSource<T>.  

#### Async Tagging

All [ITagger<T>](http://msdn.microsoft.com/en-us/library/dd885020.aspx) implementations are driven through the UI thread of Visual Studio.  Any delay in tagger implementations is felt immediately by the user.  Simple taggers can afford to be synchronous but more complex taggers must be asynchronous in order to keep the UI responsive.  EditorUtils provides the `IAsyncTaggerSource<TData, TTag>`to abstract away all of the complexities of async tagging. 

```
class MyAsyncTaggerSource : IAsyncTaggerSource<string, TextMarkerTag> { ... } 

MyAsyncTaggerSource myAsyncTaggerSource = new MyAsyncTaggerSource();
ITagger<TextMarkerTag> tagger = EditorUtilsFactory.CreateAsyncTaggerRaw(myAsyncTaggerSource);
```


#### Editor Hosting

At its core the Visual Studio editor is just a WPF control that is completely independent of Visual Studio itself.  The ability to host this control outside of Visual Studio is critical to thorough testing of VSIX plugins.  EditorUtils makes this extremely easy to do with the EditorHost feature.

```
var editorHost = new EditorHost();
var textView = editorHost.CreateTextView(); 
```

