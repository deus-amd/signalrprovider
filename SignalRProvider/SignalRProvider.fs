﻿module SignalRProvider

open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open System.Reflection

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns

[<TypeProvider>]
type ClientProvider (config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    let ns = "SignalRProvider.Hubs"
    let asm = Assembly.GetExecutingAssembly()

    let myType = ProvidedTypeDefinition(asm, ns, "MyType", Some typeof<obj>)

    // TODO - use all?
    let clientAsm =
        let lastref = config.ReferencedAssemblies |> Seq.last
        let refAsm = Assembly.LoadFrom lastref
        refAsm

    let hubs = 
        clientAsm.DefinedTypes
        |> Seq.filter (fun t -> t.GetCustomAttributes<Microsoft.AspNet.SignalR.Hubs.HubNameAttribute>() |> Seq.isEmpty |> not )

    let hubName (hubType : TypeInfo) =
        hubType.GetCustomAttribute<Microsoft.AspNet.SignalR.Hubs.HubNameAttribute>().HubName

    let makeMethod (hubName : string) (mi: MethodInfo) =
        let name = mi.Name
        let parms = mi.GetParameters() |> Seq.map (fun p -> ProvidedParameter(p.Name,  p.ParameterType (* typeof<obj> *)))

        let returnType = if mi.ReturnType.Equals(typeof<System.Void>) then typeof<unit> else mi.ReturnType
        let deferType = typedefof<JQueryDeferred<_>>.MakeGenericType(returnType)

        let objDeferType = typeof<JQueryDeferred<obj>>

        let meth = ProvidedMethod(name, parms |> List.ofSeq, objDeferType)

        let castParam (e: Expr) = Expr.Coerce(e, typeof<obj> )

        //let unbox = match <@ 1 :> obj :?> int @> with Call(e, mi, es) -> mi

        meth.InvokeCode <- (fun args -> 
            let argsArray = Expr.NewArray(typeof<obj>, args |> Seq.skip 1 |> Seq.map castParam |> List.ofSeq)

            let objExpr = <@@ let conn = ( %%args.[0] : obj) :?> HubConnection
                              conn.createHubProxy(hubName) @@>

            let invokeExpr = <@@ (%%objExpr : HubProxy).invoke(name, (%%argsArray: obj array)) @@> 
            //let objExpr = Expr.Coerce(invokeExpr, typeof<obj>)
            //Expr.Call(unbox, [invokeExpr] )

            //<@@ unbox ( (%%invokeExpr: JQueryDeferred<obj>) :> obj) @@>

            invokeExpr
            
            )
            
        meth

    let makeHubType hubType =
        let name = hubName hubType
        let props = 
            Microsoft.AspNet.SignalR.Hubs.ReflectionHelper.GetExportedHubMethods hubType
            |> Seq.map (makeMethod name) 
        let ty = ProvidedTypeDefinition(asm, ns, name, Some typeof<obj>)
        let ctor = ProvidedConstructor(parameters = [ ProvidedParameter("conn", typeof<HubConnection>) ], 
                    InvokeCode = (fun args -> <@@ (%%(args.[0]) : HubConnection) :> obj @@>))
        ty.AddMember ctor
        props |> Seq.iter (fun prop -> ty.AddMember prop)
        ty

    let hubTypes = 
        hubs
        |> Seq.map makeHubType
        |> Seq.toList

    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder(config)
        this.AddNamespace(ns, hubTypes)

[<assembly:TypeProviderAssembly>]
do ()