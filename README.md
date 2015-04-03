# Mailgun

A F# API for Mailgun.

Usage:


```
open Mailgun

let conf = Unconfigured.Create()...Configure()

Api.Messages.send ...
```

## Compiling

`bundle exec rake`

## Running Tests

`bundle exec rake tests`