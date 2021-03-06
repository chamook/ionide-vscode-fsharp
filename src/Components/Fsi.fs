namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node

open DTO
open Ionide.VSCode.Helpers

module Fsi =
    [<Emit("setTimeout($0,$1)")>]
    let setTimeout(cb, delay) : obj = failwith "JS Only"

    let mutable fsiOutput : Terminal option = None

    let isPowershell () =
        let t = workspace.getConfiguration().get("terminal.integrated.shell.windows", "")
        t.ToLower().Contains "powershell"

    let sendCd () =
        let editor = window.activeTextEditor
        let file,dir =
            if  JS.isDefined editor then
                let file = editor.document.fileName
                let dir = path.dirname file
                file,dir
            else
                let dir = workspace.rootPath
                path.join(dir, "tmp.fsx"), dir
        let msg1 = sprintf "# silentCd @\"%s\";;\n" dir
        let msg2 = (sprintf "# %d @\"%s\" \n" 1 file)


        fsiOutput |> Option.iter (fun n ->
            n.sendText(msg1, false)
            n.sendText(msg2, false)
            n.sendText(";;\n", false)
        )

    let private start () =
        try
            fsiOutput |> Option.iter (fun n -> n.dispose())
            let terminal = window.createTerminal("F# Interactive")
            fsiOutput <- Some terminal
            let fsi, clear =
                let fsiParams = 
                    workspace.getConfiguration().get("FSharp.fsiExtraParameters", Array.empty<string>) |> List.ofArray
                            
                if Environment.isWin then
                    let winParams = [ "--fsi-server-input-codepage:65001" ] @ fsiParams |> String.concat " "
 
                    if isPowershell () then
                        sprintf "cmd /c \"%s\" %s" Environment.fsi winParams, "clear"
                    else
                        sprintf "\"%s\" %s" Environment.fsi winParams, "cls"
                else
                    let nonWinParams = fsiParams |> String.concat " "
                    sprintf "%s %s" Environment.fsi nonWinParams, "clear"

            terminal.sendText(clear, true)
            terminal.sendText(fsi, true)
            setTimeout (sendCd, 1500) |> ignore
            terminal.show(true)
        with
        | _ ->
            window.showErrorMessage "Failed to spawn FSI, please ensure it's in PATH" |> ignore


    let private send (msg : string) =
        if fsiOutput.IsNone then
            start ()
            setTimeout((fun _ ->
                let msg = msg + "\n;;\n"
                fsiOutput |> Option.iter (fun fp -> fp.sendText(msg,false) )), 2000) |>ignore
        else
            let msg = msg + "\n;;\n"
            fsiOutput |> Option.iter (fun fp -> fp.sendText(msg,false) )


    let private sendLine () =
        let editor = window.activeTextEditor
        let file = editor.document.fileName
        let pos = editor.selection.start
        let line = editor.document.lineAt pos
        send line.text
        commands.executeCommand "cursorDown" |> ignore

    let private sendSelection () =
        let editor = window.activeTextEditor
        let file = editor.document.fileName

        if editor.selection.isEmpty then
            sendLine ()
        else
            let range = Range(editor.selection.anchor.line, editor.selection.anchor.character, editor.selection.active.line, editor.selection.active.character)
            let text = editor.document.getText range
            send text

    let private sendFile () =
        let editor = window.activeTextEditor
        let file = editor.document.fileName
        let text = editor.document.getText ()
        send text



    let activate (disposables: Disposable[]) =
        window.onDidChangeActiveTextEditor $ ((fun n -> if JS.isDefined n then sendCd()), (), disposables) |> ignore

        commands.registerCommand("fsi.Start", start |> unbox) |> ignore
        commands.registerCommand("fsi.SendLine", sendLine |> unbox) |> ignore
        commands.registerCommand("fsi.SendSelection", sendSelection |> unbox) |> ignore
        commands.registerCommand("fsi.SendFile", sendFile |> unbox) |> ignore

