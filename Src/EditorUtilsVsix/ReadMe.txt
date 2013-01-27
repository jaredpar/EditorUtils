This VSIX will eventually become the deployment vehicle for the EditorUtils shared library code.  It's not safe for components to simply reference EditorUtils as a DLL and deploy as a MEF component in their VSIX.  

If more than one VSIX registers a DLL as a MEF component then Visual Studio will add it's components to the MEF export list more than once as well (one for each time it's listed in a manifest as a MEF component).  This is true even if the DLL is strongly named.  

The end effect is that every export happens N times instead of 1.  This completely breaks any singleton style exports as an Import attribute no longer works (MEF sees a collection at this point, not a singleton).  

The only way to safely deploy the a shared library as a MEF component is to create a VSIX soley for deploying the shared library.  This VSIX is the only one that registers the DLL as a MEF component.  The projects which depend on the Util continue to reference the DLL as a normal reference.  But instead of listing it as a MEF component, they list the VSIX reference which will deploy the DLL + MEF register.  This can be done in such a way that there is still a single end component installation (see below for an example)

  <References>
    <Reference Id="EditorUtilsVsix.5b3b8756-e1d7-4f07-bc0f-8f995063a6c4" MinVersion="1.0.0.0">
      <Name>EditorUtilsVsix</Name>
      <MoreInfoUrl>http://blogs.msdn.com/b/jaredpar</MoreInfoUrl>
      <VsixPath>EditorUtilsVsix.vsix</VsixPath>
    </Reference>
  </References>
