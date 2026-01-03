# Output Schema

```json
{
  "schemaVersion": "1",
  "nodes": [
    {
      "id": 1,
      "kind": "Type",
      "name": "Greeter",
      "fullyQualifiedName": "SampleApp.Greeter",
      "documentPath": "Program.cs",
      "location": {
        "filePathRelative": "Program.cs",
        "startLine": 5,
        "startColumn": 1,
        "endLine": 9,
        "endColumn": 2
      }
    }
  ],
  "edges": [
    {
      "id": 10,
      "kind": "DeclaredAt",
      "sourceId": 2,
      "targetId": 1,
      "location": {
        "filePathRelative": "Program.cs",
        "startLine": 5,
        "startColumn": 1,
        "endLine": 9,
        "endColumn": 2
      }
    }
  ]
}
```
