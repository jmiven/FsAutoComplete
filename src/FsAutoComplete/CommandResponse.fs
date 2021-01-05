namespace FsAutoComplete

open System

open FSharp.Compiler
open FSharp.Compiler.SourceCodeServices
open Ionide.ProjInfo.ProjectSystem
open Ionide.ProjInfo.ProjectSystem.WorkspacePeek
open FSharp.UMX

module internal CompletionUtils =
  let getIcon (glyph : FSharpGlyph) =
    match glyph with
    | FSharpGlyph.Class -> ("Class", "C")
    | FSharpGlyph.Constant -> ("Constant", "Cn")
    | FSharpGlyph.Delegate -> ("Delegate", "D")
    | FSharpGlyph.Enum -> ("Enum", "E")
    | FSharpGlyph.EnumMember -> ("Property", "P")
    | FSharpGlyph.Event -> ("Event", "e")
    | FSharpGlyph.Exception -> ("Exception", "X")
    | FSharpGlyph.Field -> ("Field", "F")
    | FSharpGlyph.Interface -> ("Interface", "I")
    | FSharpGlyph.Method -> ("Method", "M")
    | FSharpGlyph.OverridenMethod -> ("Method", "M")
    | FSharpGlyph.Module -> ("Module", "N")
    | FSharpGlyph.NameSpace -> ("Namespace", "N")
    | FSharpGlyph.Property -> ("Property", "P")
    | FSharpGlyph.Struct -> ("Struct", "S")
    | FSharpGlyph.Typedef -> ("Class", "C")
    | FSharpGlyph.Type -> ("Type", "T")
    | FSharpGlyph.Union -> ("Type", "T")
    | FSharpGlyph.Variable -> ("Variable", "V")
    | FSharpGlyph.ExtensionMethod -> ("Extension Method", "M")
    | FSharpGlyph.Error -> ("Error", "E")


  let getEnclosingEntityChar = function
    | FSharpEnclosingEntityKind.Namespace -> "N"
    | FSharpEnclosingEntityKind.Module -> "M"
    | FSharpEnclosingEntityKind.Class -> "C"
    | FSharpEnclosingEntityKind.Exception -> "E"
    | FSharpEnclosingEntityKind.Interface -> "I"
    | FSharpEnclosingEntityKind.Record -> "R"
    | FSharpEnclosingEntityKind.Enum -> "En"
    | FSharpEnclosingEntityKind.DU -> "D"

module internal ClassificationUtils =

  // See https://code.visualstudio.com/api/language-extensions/semantic-highlight-guide#semantic-token-scope-map for the built-in scopes
  // if new token-type strings are added here, make sure to update the 'legend' in any downstream consumers.
  let map (t: SemanticClassificationType) : string =
      match t with
      | SemanticClassificationType.Operator -> "operator"
      | SemanticClassificationType.ReferenceType
      | SemanticClassificationType.Type
      | SemanticClassificationType.TypeDef
      | SemanticClassificationType.ConstructorForReferenceType -> "type"
      | SemanticClassificationType.ValueType
      | SemanticClassificationType.ConstructorForValueType -> "struct"
      | SemanticClassificationType.UnionCase
      | SemanticClassificationType.UnionCaseField -> "enumMember"
      | SemanticClassificationType.Function
      | SemanticClassificationType.Method
      | SemanticClassificationType.ExtensionMethod -> "function"
      | SemanticClassificationType.Property -> "property"
      | SemanticClassificationType.MutableVar
      | SemanticClassificationType.MutableRecordField -> "mutable"
      | SemanticClassificationType.Module
      | SemanticClassificationType.Namespace -> "namespace"
      | SemanticClassificationType.Printf -> "regexp"
      | SemanticClassificationType.ComputationExpression -> "cexpr"
      | SemanticClassificationType.IntrinsicFunction -> "function"
      | SemanticClassificationType.Enumeration -> "enum"
      | SemanticClassificationType.Interface -> "interface"
      | SemanticClassificationType.TypeArgument -> "typeParameter"
      | SemanticClassificationType.DisposableTopLevelValue
      | SemanticClassificationType.DisposableLocalValue
      | SemanticClassificationType.DisposableType -> "disposable"
      | SemanticClassificationType.Literal -> "variable.readonly.defaultLibrary"
      | SemanticClassificationType.RecordField
      | SemanticClassificationType.RecordFieldAsFunction -> "property.readonly"
      | SemanticClassificationType.Exception
      | SemanticClassificationType.Field
      | SemanticClassificationType.Event
      | SemanticClassificationType.Delegate
      | SemanticClassificationType.NamedArgument -> "member"
      | SemanticClassificationType.Value
      | SemanticClassificationType.LocalValue -> "variable"
      | SemanticClassificationType.Plaintext -> "text"

module CommandResponse =
  open FSharp.Compiler.Range

  type ResponseMsg<'T> =
    {
      Kind: string
      Data: 'T
    }

  type ResponseError<'T> =
    {
      Code: int
      Message: string
      AdditionalData: 'T
    }

  [<RequireQualifiedAccess>]
  type ErrorCodes =
    | GenericError = 1
    | ProjectNotRestored = 100
    | ProjectParsingFailed = 101
    | GenericProjectError = 102

  [<RequireQualifiedAccess>]
  type ErrorData =
    | GenericError
    | ProjectNotRestored of ProjectNotRestoredData
    | ProjectParsingFailed of ProjectParsingFailedData
    | GenericProjectError of ProjectData
  and ProjectNotRestoredData = { Project: ProjectFilePath }
  and ProjectParsingFailedData = { Project: ProjectFilePath }
  and ProjectData = { Project: ProjectFilePath }

  type ProjectResponseInfoDotnetSdk =
    {
      IsTestProject: bool
      Configuration: string
      IsPackable: bool
      TargetFramework: string
      TargetFrameworkIdentifier: string
      TargetFrameworkVersion: string
      RestoreSuccess: bool
      TargetFrameworks: string list
      RunCmd: RunCmd option
      IsPublishable: bool option
    }
  and [<RequireQualifiedAccess>] RunCmd = { Command: string; Arguments: string }

  type ProjectLoadingResponse =
    {
      Project: ProjectFilePath
    }

  type ProjectReference =
    { RelativePath: string
      ProjectFileName: string }

  type PackageReference =
    { Name: string
      Version: string
      FullPath: string }

  type ProjectResponse =
    {
      Project: ProjectFilePath
      Files: List<SourceFilePath>
      Output: string
      ProjectReferences: List<ProjectReference>
      PackageReferences: List<PackageReference>
      References: List<ProjectFilePath>
      OutputType: ProjectOutputType
      Info: ProjectResponseInfoDotnetSdk
      Items: List<ProjectResponseItem>
      AdditionalInfo: Map<string, string>
    }
  and ProjectOutputType =
    | Library
    | Exe
    | Custom of string
  and ProjectResponseItem =
    {
      Name: string
      FilePath: string
      VirtualPath: string
      Metadata: Map<string, string>
    }

  type DocumentationDescription =
    {
      XmlKey: string
      Constructors: string list
      Fields: string list
      Functions: string list
      Interfaces: string list
      Attributes: string list
      Types: string list
      Signature: string
      Comment: string
      Footer: string
    }

  type CompilerLocationResponse =
    {
      Fsc: string option
      Fsi: string option
      MSBuild: string option
      SdkRoot: string option
    }

  type FSharpErrorInfo =
    {
      FileName: string
      StartLine:int
      EndLine:int
      StartColumn:int
      EndColumn:int
      Severity:FSharpErrorSeverity
      Message:string
      Subcategory:string
    }
    static member IsIgnored(e:FSharp.Compiler.SourceCodeServices.FSharpErrorInfo) =
        // FST-1027 support in Fake 5
        e.ErrorNumber = 213 && e.Message.StartsWith "'paket:"
    static member OfFSharpError(e:FSharp.Compiler.SourceCodeServices.FSharpErrorInfo) =
      {
        FileName = e.FileName
        StartLine = e.StartLineAlternate
        EndLine = e.EndLineAlternate
        StartColumn = e.StartColumn + 1
        EndColumn = e.EndColumn + 1
        Severity = e.Severity
        Message = e.Message
        Subcategory = e.Subcategory
      }


  type Declaration =
    {
      UniqueName: string
      Name: string
      Glyph: string
      GlyphChar: string
      IsTopLevel: bool
      Range: Range.range
      BodyRange : Range.range
      File : string
      EnclosingEntity: string
      IsAbstract: bool
    }
    static member OfDeclarationItem(e:FSharpNavigationDeclarationItem, fn) =
      let (glyph, glyphChar) = CompletionUtils.getIcon e.Glyph
      {
        UniqueName = e.UniqueName
        Name = e.Name
        Glyph = glyph
        GlyphChar = glyphChar
        IsTopLevel = e.IsSingleTopLevel
        Range = e.Range
        BodyRange = e.BodyRange
        File = fn
        EnclosingEntity = CompletionUtils.getEnclosingEntityChar e.EnclosingEntityKind
        IsAbstract = e.IsAbstract
      }

  type DeclarationResponse = {
      Declaration : Declaration;
      Nested : Declaration []
  }

  type Parameter = {
    Name : string
    Type : string
  }

  type SignatureData = {
    OutputType : string
    Parameters : Parameter list list
    Generics : string list
  }

  type WorkspacePeekResponse = {
    Found: WorkspacePeekFound list
  }
  and WorkspacePeekFound =
    | Directory of WorkspacePeekFoundDirectory
    | Solution of WorkspacePeekFoundSolution
  and WorkspacePeekFoundDirectory = {
    Directory: string
    Fsprojs: string list
  }
  and WorkspacePeekFoundSolution = {
    Path: string
    Items: WorkspacePeekFoundSolutionItem list
    Configurations: WorkspacePeekFoundSolutionConfiguration list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItem = {
    Guid: Guid
    Name: string
    Kind: WorkspacePeekFoundSolutionItemKind
  }
  and WorkspacePeekFoundSolutionItemKind =
    | MsbuildFormat of WorkspacePeekFoundSolutionItemKindMsbuildFormat
    | Folder of WorkspacePeekFoundSolutionItemKindFolder
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItemKindMsbuildFormat = {
    Configurations: WorkspacePeekFoundSolutionConfiguration list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItemKindFolder = {
    Items: WorkspacePeekFoundSolutionItem list
    Files: FilePath list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionConfiguration = {
    Id: string
    ConfigurationName: string
    PlatformName: string
  }

  type WorkspaceLoadResponse = {
    Status: string
    }

  type CompileResponse = {
    Code: int
    Errors: FSharpErrorInfo []
  }

  type FsdnResponse = {
    Functions: string list
  }

  type DotnetNewListResponse = {
    Installed : DotnetNewTemplate.Template list
  }

  type DotnetNewGetDetailsResponse = {
    Detailed : DotnetNewTemplate.DetailedTemplate
  }

  type DotnetNewCreateCliResponse = {
    CommandName : string
    ParameterStr : string
  }

  type HighlightingRange = { Range: LanguageServerProtocol.Types.Range ; TokenType: string }

  type HighlightingResponse = {
    Highlights: HighlightingRange []
  }

  type PieplineHint = {
    Line: int
    Types: string []
    PrecedingNonPipeExprLine : int option
  }

  let private errorG (serialize : Serializer) (errorData: ErrorData) message =
    let inline ser code data =
        serialize { Kind = "error"; Data = { Code = (int code); Message = message; AdditionalData = data }  }
    match errorData with
    | ErrorData.GenericError -> ser (ErrorCodes.GenericError) (obj())
    | ErrorData.GenericProjectError d -> ser (ErrorCodes.GenericProjectError) d
    | ErrorData.ProjectNotRestored d -> ser (ErrorCodes.ProjectNotRestored) d
    | ErrorData.ProjectParsingFailed d -> ser (ErrorCodes.ProjectParsingFailed) d

  let project (serialize : Serializer) (projectResult: ProjectResult) =
    let info = projectResult.Extra.ProjectSdkInfo
    let projectInfo =
        {
          IsTestProject = info.IsTestProject
          Configuration = info.Configuration
          IsPackable = info.IsPackable
          TargetFramework = info.TargetFramework
          TargetFrameworkIdentifier = info.TargetFrameworkIdentifier
          TargetFrameworkVersion = info.TargetFrameworkVersion
          RestoreSuccess = info.RestoreSuccess
          TargetFrameworks = info.TargetFrameworks
          RunCmd =
            match info.RunCommand, info.RunArguments with
            | Some cmd, Some args -> Some { RunCmd.Command = cmd; Arguments = args }
            | Some cmd, None -> Some { RunCmd.Command = cmd; Arguments = "" }
            | _ -> None
          IsPublishable = info.IsPublishable
        }
    let mapItemResponse (p: Ionide.ProjInfo.ProjectViewerItem) : ProjectResponseItem =
      match p with
      |Ionide.ProjInfo.ProjectViewerItem.Compile (fullpath, extraInfo) ->
        { ProjectResponseItem.Name = "Compile"
          ProjectResponseItem.FilePath = fullpath
          ProjectResponseItem.VirtualPath = extraInfo.Link
          ProjectResponseItem.Metadata = Map.empty }

    let projectData =
      { Project = projectResult.ProjectFileName
        Files = projectResult.ProjectFiles
        Output = match projectResult.OutFileOpt with Some x -> x | None -> "null"
        ProjectReferences = projectResult.Extra.ReferencedProjects |> List.map (fun n -> {RelativePath = n.RelativePath; ProjectFileName = n.ProjectFileName})
        PackageReferences = projectResult.Extra.PackageReferences |> List.map (fun n -> {Name = n.Name; Version = n.Version; FullPath = n.FullPath})
        References = List.sortBy IO.Path.GetFileName projectResult.References
        OutputType =
          match projectResult.Extra.ProjectOutputType with
          |Ionide.ProjInfo.Types.ProjectOutputType.Library -> Library
          |Ionide.ProjInfo.Types.ProjectOutputType.Exe -> Exe
          |Ionide.ProjInfo.Types.ProjectOutputType.Custom outType -> Custom outType
        Info = projectInfo
        Items = projectResult.ProjectItems |> List.map mapItemResponse
        AdditionalInfo = projectResult.Additionals }
    serialize { Kind = "project"; Data = projectData }

  let projectError (serialize : Serializer) errorDetails =
    let rec getMessageLines errorDetails =
      match errorDetails with
      |Ionide.ProjInfo.Types.ProjectNotFound (_) -> ["couldn't find project"]
      |Ionide.ProjInfo.Types.LanguageNotSupported (_) -> [sprintf "this project is not supported, only fsproj"]
      |Ionide.ProjInfo.Types.ProjectNotLoaded (_) -> [sprintf "this project was not loaded due to some internal error"]
      |Ionide.ProjInfo.Types.MissingExtraProjectInfos _ -> [sprintf "this project was not loaded because ExtraProjectInfos were missing"]
      |Ionide.ProjInfo.Types.InvalidExtraProjectInfos (_, err) ->  [sprintf "this project was not loaded because ExtraProjectInfos were invalid: %s" err]
      |Ionide.ProjInfo.Types.ReferencesNotLoaded (_, referenceErrors) ->

        [ yield sprintf "this project was not loaded because some references could not be loaded:"
          yield!
            referenceErrors
              |> Seq.collect (fun (projPath, er) ->
                [ yield sprintf "  - %s:" projPath
                  yield! getMessageLines er |> Seq.map (fun line -> sprintf "    - %s" line)]) ]
      |Ionide.ProjInfo.Types.GenericError (_, errorMessage) -> [errorMessage]
      |Ionide.ProjInfo.Types.ProjectNotRestored _ -> ["Project not restored"]
    let msg = getMessageLines errorDetails |> fun s -> String.Join("\n", s)
    match errorDetails with
    |Ionide.ProjInfo.Types.ProjectNotFound (project) -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.LanguageNotSupported (project) -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.ProjectNotLoaded project -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.MissingExtraProjectInfos project -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.InvalidExtraProjectInfos (project, _) -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.ReferencesNotLoaded (project, _) -> errorG serialize (ErrorData.GenericProjectError { Project = project }) msg
    |Ionide.ProjInfo.Types.GenericError (project, _) -> errorG serialize (ErrorData.ProjectParsingFailed { Project = project }) msg
    |Ionide.ProjInfo.Types.ProjectNotRestored project -> errorG serialize (ErrorData.ProjectNotRestored { Project = project }) msg

  let projectLoading (serialize : Serializer) projectFileName =
    serialize { Kind = "projectLoading"; Data = { ProjectLoadingResponse.Project = projectFileName } }

  let workspacePeek (serialize : Serializer) (found: WorkspacePeek.Interesting list) =
    let mapInt i =
        match i with
        | WorkspacePeek.Interesting.Directory (p, fsprojs) ->
            WorkspacePeekFound.Directory { WorkspacePeekFoundDirectory.Directory = p; Fsprojs = fsprojs }
        | WorkspacePeek.Interesting.Solution (p, sd) ->
            let rec item (x:Ionide.ProjInfo.InspectSln.SolutionItem) =
                let kind =
                    match x.Kind with
                    |Ionide.ProjInfo.InspectSln.SolutionItemKind.Unknown
                    |Ionide.ProjInfo.InspectSln.SolutionItemKind.Unsupported ->
                        None
                    |Ionide.ProjInfo.InspectSln.SolutionItemKind.MsbuildFormat msbuildProj ->
                        Some (WorkspacePeekFoundSolutionItemKind.MsbuildFormat {
                            WorkspacePeekFoundSolutionItemKindMsbuildFormat.Configurations = []
                        })
                    |Ionide.ProjInfo.InspectSln.SolutionItemKind.Folder(children, files) ->
                        let c = children |> List.choose item
                        Some (WorkspacePeekFoundSolutionItemKind.Folder {
                            WorkspacePeekFoundSolutionItemKindFolder.Items = c
                            Files = files
                        })
                kind
                |> Option.map (fun k -> { WorkspacePeekFoundSolutionItem.Guid = x.Guid; Name = x.Name; Kind = k })
            let items = sd.Items |> List.choose item
            WorkspacePeekFound.Solution { WorkspacePeekFoundSolution.Path = p; Items = items; Configurations = [] }

    let data = { WorkspacePeekResponse.Found = found |> List.map mapInt }
    serialize { Kind = "workspacePeek"; Data = data }

  let workspaceLoad (serialize : Serializer) finished =
    let data =
        if finished then "finished" else "started"
    serialize { Kind = "workspaceLoad"; Data = { WorkspaceLoadResponse.Status = data } }

  let projectLoad (serialize : Serializer) finished =
    let data =
        if finished then "finished" else "started"
    serialize { Kind = "projectLoad"; Data = { WorkspaceLoadResponse.Status = data } }

  let signatureData (serialize : Serializer) ((typ, parms, generics) : string * ((string * string) list list) * string list) =
    let pms =
      parms
      |> List.map (List.map (fun (n, t) -> { Name= n; Type = t }))
    serialize { Kind = "signatureData"; Data = { Parameters = pms; OutputType = typ; Generics = generics } }

  let help (serialize : Serializer) (data : string) =
    serialize { Kind = "help"; Data = data }

  let fsdn (serialize : Serializer) (functions : string list) =
    let data = { FsdnResponse.Functions = functions }
    serialize { Kind = "fsdn"; Data = data }

  let dotnetnewlist (serialize : Serializer) (installedTemplate : DotnetNewTemplate.Template list) =
    serialize { Kind = "dotnetnewlist"; Data = installedTemplate }

  let dotnetnewgetDetails (serialize : Serializer) (detailedTemplate : DotnetNewTemplate.DetailedTemplate) =
    let data = { DotnetNewGetDetailsResponse.Detailed = detailedTemplate }
    serialize { Kind = "dotnetnewgetDetails"; Data = data }

  let dotnetnewCreateCli (serialize : Serializer) (commandName : string, parameterStr : string) =
    serialize { Kind = "dotnetnewCreateCli";
                Data = { CommandName = commandName
                         ParameterStr = parameterStr} }

  let declarations (serialize : Serializer) (decls : (FSharpNavigationTopLevelDeclaration * string<LocalPath>) []) =
     let decls' =
      decls |> Array.map (fun (d, fn) ->
        { Declaration = Declaration.OfDeclarationItem (d.Declaration, UMX.untag fn);
          Nested = d.Nested |> Array.map ( fun a -> Declaration.OfDeclarationItem(a, UMX.untag fn))
        })
     serialize { Kind = "declarations"; Data = decls' }

  let formattedDocumentation (serialize : Serializer) (tip, xmlSig, signature, footer, cn) =
    let data =
      match tip, xmlSig  with
      | Some tip, _ ->
        TipFormatter.formatDocumentation tip signature footer cn |> List.map(List.map(fun (n,cns, fds, funcs, intf, attrs,ts, m,f, cn) -> {XmlKey = cn; Constructors = cns |> Seq.toList; Fields = fds |> Seq.toList; Functions = funcs |> Seq.toList; Interfaces = intf |> Seq.toList; Attributes = attrs |> Seq.toList; Types = ts |> Seq.toList; Footer =f; Signature = n; Comment = m} ))
      | _, Some (xml, assembly) ->
        TipFormatter.formatDocumentationFromXmlSig xml assembly signature footer cn |> List.map(List.map(fun (n,cns, fds, funcs, intf, attrs,ts, m,f, cn) -> {XmlKey = cn; Constructors = cns |> Seq.toList; Fields = fds |> Seq.toList; Functions = funcs |> Seq.toList; Interfaces = intf |> Seq.toList; Attributes = attrs |> Seq.toList; Types = ts |> Seq.toList; Footer =f; Signature = n; Comment = m} ))
      | _ -> failwith "Shouldn't happen"
    serialize { Kind = "formattedDocumentation"; Data = data }

  let formattedDocumentationForSymbol (serialize : Serializer) xml assembly (xmlDoc : string list) (signature, footer, cn) =
    let data = TipFormatter.formatDocumentationFromXmlSig xml assembly signature footer cn |> List.map(List.map(fun (n,cns, fds, funcs, intf, attrs, ts, m,f, cn) ->
      let m = if String.IsNullOrWhiteSpace m then xmlDoc |> String.concat "\n" else m
      {XmlKey = cn; Constructors = cns |> Seq.toList; Fields = fds |> Seq.toList; Functions = funcs |> Seq.toList; Interfaces = intf |> Seq.toList; Attributes = attrs |> Seq.toList; Types = ts |> Seq.toList; Footer =f; Signature = n; Comment = m} ))
    serialize { Kind = "formattedDocumentation"; Data = data }

  let typeSig (serialize : Serializer) (tip) =
    let data = TipFormatter.extractSignature tip
    serialize { Kind = "typesig"; Data = data }

  let compilerLocation (serialize : Serializer) fsc fsi msbuild sdkRoot =
    let data = { Fsi = fsi; Fsc = fsc; MSBuild = msbuild; SdkRoot = sdkRoot }
    serialize { Kind = "compilerlocation"; Data = data }

  let compile (serialize : Serializer) (errors,code) =
    serialize { Kind = "compile"; Data = {Code = code; Errors = Array.map FSharpErrorInfo.OfFSharpError errors}}

  let fakeTargets (serialize : Serializer) (targets : FakeSupport.GetTargetsResult) =
     serialize targets

  let fakeRuntime (serialize : Serializer) (runtimePath : string) =
     serialize { Kind = "fakeRuntime"; Data = runtimePath }

  let fsharpLiterate (serialize: Serializer) (content: string) =
    serialize { Kind = "fsharpLiterate"; Data = content}

  let pipelineHint (serialize: Serializer) (content: (int * int option * string list) []) =
    let ctn =
      content |> Array.map (fun (l, pnp, tt) -> {
        Line = l
        Types = Array.ofList tt
        PrecedingNonPipeExprLine = pnp
      })
    serialize { Kind = "pipelineHint"; Data = ctn }
