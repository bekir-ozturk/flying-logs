## Limitations 

> [!NOTE] 
> This page is work in progress.

flying-logs is not the best tool for every scenario. It aims to be very efficient and allocation-free. Any feature that contradicts these goals would be out of the scope of this project. After all, there are already libraries that deliver a wide range of features for those who can afford to sacrifice a little bit of performance.

That being said, the limitations below are not set in stone. It may be possible to deliver some of them in the future either fully or partially. If you are planning to switch, knowing these could help you understand how difficult it would be to leave your current logging library for flying-logs.

### Generic types cannot be used in logs unless they are cast to a public type
Log methods generated by the source generator live in static classes. Due to them being declared in a different context, the generic types in your class will be unknown to the log method you are calling. Therefore, they will be treated as 'object's.

### There is no support for scopes.
TODO

### The only encoding is UTF8.
You can't write to a file in ASCII unless you write your own formatter and pay the cost of reencoding.

TODO

### No support for expanding Arrays.
TODO

### No support for anonymous types.
TODO