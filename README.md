# Mailgun

A F# API for Mailgun.

Usage:


``` fsharp
open System.Net.Mail
open Mailgun.Api
open HttpClient
open Fuchu

let env key =
  match System.Environment.GetEnvironmentVariable key with
  | null -> Tests.failtestf "provide env var %s for tests" key
  | value -> value

let conf = { apiKey = env "MAILGUN_API_KEY" }
let msg =
  { from = MailAddress "henrik@sandbox60931.mailgun.org"
    ``to`` = [ MailAddress "henrik@haf.se" ]
    cc = []
    bcc = []
    subject = "Hello World åäö"
    body    = TextBody "Hi!

Would you like to go to the prom with me?

XOXOXOXOX
Yourself."
    attachments = [] }
let settings = { SendOpts.Create "sandbox60931.mailgun.org" with testMode = true }
match Messages.send conf settings msg |> Async.RunSynchronously with
| Result (_, resp) ->
  Assert.Equal("correct status code", 200, resp.StatusCode)
| other ->
  Tests.failtestf "got %A, but expected valid result" other
```

## Compiling

`bundle exec rake`

## Running Tests

`bundle exec rake tests`