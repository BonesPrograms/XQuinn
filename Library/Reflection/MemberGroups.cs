
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using static XQuinn.Numerics.BytesLittleEndian;
using static XQuinn.Reflection.MemberGroup;

namespace XQuinn.Reflection;



//Must be power of 2 to work with flags... need to research why

[Flags] //flags are used for querying, so that you can get members of multiple different kinds from my reflection collections via enum input
public enum MemberGroup //each reflectioninfo object will only have one flag
{
    _invalid = 0,
    Field = 8,
    Property = 16,
    Method = 32,
    Event = 64,
    Constructor = 128, //why does it say all is already 127 if i make this 127.. .wait a minute... math... thats why...
    All = Property | Field | Constructor | Method | Event
}


///I used to use this class more, I dont anymore, but it was useful then, so it remains incase it is ever useful again
public static class MemberGroups
{

    public static readonly ImmutableArray<MemberGroup> Groups = [Field, Property, Method, Event, Constructor];

    public static MemberGroup GetGroup(MemberInfo info) => info switch
    {
        ConstructorInfo => Constructor,
        FieldInfo => Field,
        PropertyInfo => Property,
        EventInfo => Event,
        MethodInfo => Method,
        // Type => null, //Types are not supported, return type is nullable so that way you are constantly reminded if you have nullable context turned on
        Type or _ => throw new NotSupportedException($"{info.MemberType} not supported.")
    };
}