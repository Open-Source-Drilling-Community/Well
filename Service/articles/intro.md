---
title: "How to use the Well microservice?"
output: html_document
---

Typical Usage
===
1. Upload a new Well using the `Post` web api method.
2. Call the `Get` method with the identifier of the uploaded Well as argument. 
The return Json object contains the Well description.
3. Optionally send a `Delete` request with the identifier of the Well in order to delete the Well if you do not 
want to keep the Well uploaded on the microservice.


