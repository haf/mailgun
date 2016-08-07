module Mailgun.Api.Tests

open System
open System.Net.Mail
open Mailgun.Api
open HttpFs.Client
open Fuchu
open Hopac

let env key =
  match System.Environment.GetEnvironmentVariable key with
  | null -> Tests.failtestf "provide env var %s for tests" key
  | value -> value

[<Tests>]
let sending =
  // note that since you don't own my domains and Mailgun account it you'll have
  // to alter these tests to run on your machine; send a PR with all that parametised

  let testImagePath = (System.Reflection.Assembly.GetExecutingAssembly().Location + "/../../../testImage.png") |> System.IO.Path.GetFullPath
  let testFileImg : File =
    ("testImage.png", ContentType.create("image", "png"), Binary (System.IO.File.ReadAllBytes testImagePath))

  testList "can send messages" [
    testCase "send to myself" <| fun _ ->
      let conf = { apiKey = env "MAILGUN_API_KEY" }
      let msg =
        { from = MailAddress "henrik@sandbox60931.mailgun.org"
          ``to`` = [ MailAddress "henrik@haf.se" ]
          cc = []
          bcc = []
          subject = "Hello World åäö"
          body    = HtmlBody "Hi!

Would you like to go to the prom with me?
Image test:
<html><img src='cid:testImage.png' alt='no image could be loaded'></html>
XOXOXOXOX
Yourself."
          attachments = []
          inlineImgs = [testFileImg] }
      let settings = { SendOpts.Create "sandbox60931.mailgun.org" with testMode = true }
      match Messages.send conf settings msg |> Hopac.run with
      | Result resp ->
        Assert.Equal("correct status code", 200, resp.statusCode)
        (resp :> IDisposable).Dispose()
      | other ->
        Tests.failtestf "got %A, but expected valid result" other
    ]

[<EntryPoint>]
let main argv =
  Tests.defaultMainThisAssembly argv