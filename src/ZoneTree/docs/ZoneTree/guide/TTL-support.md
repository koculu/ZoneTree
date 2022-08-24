## How to setup ZoneTree with TTL support?

Time-to-live (TTL) is a value for the period of time that a data, should exist before being discarded.

It is straight forward with ZoneTree to implement TTL for key-value pairs.

Following code demonstrates a TTL ZoneTree database with absolute expiration.

```C#
using var zoneTree = new ZoneTreeFactory<int, TTLValue<int>>()    
    .SetValueSerializer(new StructSerializer<TTLValue<int>>())
    .SetIsValueDeletedDelegate((in TTLValue<int> x) => x.IsExpired)
    .SetMarkValueDeletedDelegate(void (ref TTLValue<int> x) => x.Expire())
    .OpenOrCreate();

var expiration = DateTime.UtcNow.AddSeconds(30);

zoneTree.Upsert(5, new TTLValue<int>(99, expiration));
```


Following code demonstrates a TTL ZoneTree database with sliding expiration.

```C#
using var zoneTree = new ZoneTreeFactory<int, TTLValue<int>>()    
    .SetValueSerializer(new StructSerializer<TTLValue<int>>())
    .SetIsValueDeletedDelegate((in TTLValue<int> x) => x.IsExpired)
    .SetMarkValueDeletedDelegate(void (ref TTLValue<int> x) => x.Expire())
    .OpenOrCreate();

var expiration = DateTime.UtcNow.AddSeconds(30);

zoneTree.Upsert(5, new TTLValue<int>(99, expiration));

var found = zoneTree.TryGetAndUpdate(
    5,
    out value,
    bool (ref TTLValue<int> v) => 
        v.SlideExpiration(TimeSpan.FromSeconds(15)));  
```

You can use build in [TTLValue](/docs/ZoneTree/api/Tenray.ZoneTree.PresetTypes.TTLValue-1.html) or you can write your own with custom expiration logic.