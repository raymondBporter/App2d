# JSON contracts

Use the cached, read-only instances in `JsonProfiles`. File output is indented. Do not create options at a serialization call site or change a shared instance's converters/settings.

| Profile | Contract / callers |
| --- | --- |
| `Indented` | Declared property names; reports, ImageLab exports and Beamwright progress |
| `Fields` | Declared names plus public fields; value-observation round trips |
| `CamelCase` | Case-sensitive camelCase; general typed data |
| `SparseCamelCase` | CamelCase with absent optional values omitted; migration reference captures |
| `CaseInsensitive` | Declared names with case-tolerant input; imported Beamwright campaign and spell tuning |
| `CamelCaseInsensitive` | CamelCase with case-tolerant input and strict numbers; player saves |
| `Web` | CamelCase, case-tolerant input and quoted numbers; existing animation/tileset/player-geometry manifests |
| `SnakeCase` | The external music-stem manifest's snake_case names |
| `StrictCamelCase` | Exact camelCase names, strict numbers and no unknown members; owned settings and level sidecars |
| `World` | StrictCamelCase plus Vector2 coordinates and omitted nulls; the world document, its generators and tests |
| `Geometry` | Web numeric/case rules, strict members, coordinate converter and unordered metadata; geometry values |
| `Puppet` | Geometry's rules with the prototype file's original metadata ordering |
| `Authored` | Parts, rigs, clips, templates and profiles; strict numbers/members, case-tolerant names, schema-aware defaults and keyframe components |
| `AuthoredSnapshot` (internal) | Authored with derived extrusion meshes retained for undo |

`AuthoredJson.Options`, `GeometryJson2D.JsonOptions` and `PuppetDefinition.JsonOptions` expose the corresponding shared instances. Their existing entry points keep the same contracts. `AuthoredJson` retains its character namespace because its default-omission rules apply to character schema types.

`Vector2JsonConverter` lives here and respects its profile's naming, case, number and unknown-member policies. Geometry's kind/interface converter stays in `Geometry/Serialization`: its shape/curve vocabulary belongs to that subsystem and is selected by the geometry types' converter attributes.

Tests that intentionally exercise other contracts cache those options in static fields. The observation round-trip test's polymorphic event codec remains test-only; it does not define a production transport. The standalone ClothLab HTML exporter uses the framework's cached `JsonSerializerOptions.Web` singleton for its compact embedded JavaScript data.

For a genuinely different format, define a named cached profile here. Clone an existing profile once, configure it before use, and make it read-only. Keep third-party casing/number rules explicit rather than silently changing their contract to the authored-file schema.
