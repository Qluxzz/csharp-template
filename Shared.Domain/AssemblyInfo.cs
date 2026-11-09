using StronglyTypedIds;

// Applies the custom "guid-json" template (guid-json.typedid) to every [StronglyTypedId] in this
// assembly, so IDs serialize as prefixed strings like "example_<guid>".
[assembly: StronglyTypedIdDefaults("guid-json")]
