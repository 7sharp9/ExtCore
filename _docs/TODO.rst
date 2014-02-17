TODO for ExtCore 1.0
####################

Library_
    General, library-wide TODO items.

General_
    General-purpose modules, such as ``Operators`` and ``ExtraTopLevelOperators``.

Strings_
    String manipulation.
    
`Collections and Data Structures`_
    General-purpose and specialized collections and data structures.

Caching_
    Map-like data structures implementing various caching protocols.

`Workflow Collections`_
    Collections modules from the F# Core Library (e.g., ``Array``) and ExtCore,
    adapted ("lifted") for compatibility with various F# workflows.

Parallel_
    Functions implementing parallelized operations over collections.


Library
=======

Documentation
-------------
- Fill out missing documentation comments throughout the library. This is not limited to summaries --
  we also need to fill in descriptions for parameters (``<param>``), return values (``<returns>``),
  type parameters (``<typeparam>``) and so on.

- Generate some nice-looking documentation from the XML documentation file (``ExtCore.XML``) generated
  when the library is compiled. This needs to be done in a way that can be automated so it can be
  added to the build process.


Build
-----
- Publish debugging symbols (e.g., on SymbolSource) and add ``ExtCore.pdb`` to the NuGet package.
- Should we publish separate NuGet packages for F# 2.0 and F# 3.0 versions of the library?

  - If not, we should still include some logic in the NuGet package which adds a binding redirect
    when the package is added to an F# 3.0 project.

- Write some better build scripts using MSBuild or FAKE so we can easily compile multiple
  versions of the library targeting different CLR versions for distribution in the NuGet package.


Tests
-----
- Fill out the unit tests and implement more tests with FsCheck.

  - Currently, there are approximately 1200 unit tests in ``ExtCore.Tests``, around 300 of which
    are just stubs (they only call ``Assert.Ignore()``). There are a small number (~10) failing
    tests which need to be investigated and fixed.

- Clean up the unit tests imported from the F# distribution and the F# PowerPack. This includes:

  - The ``Map`` and ``Set`` type and module tests which were imported and adapted for our custom
    map and set types (e.g., ``IntSet``).
  - The ``Array`` module tests which were imported and adapted for the ``vector`` type.
  - The tests for ``ResizeArray`` and ``LazyList`` which were imported from the F# PowerPack.

- Where possible, change the equality/equivalance assertions on collections to use the helper functions
  in the ``Collections`` module (they provide better error messages on failure).

- Strengthen some of the IntSet, IntMap, etc. unit tests so they use ``assertSame`` to check that
  the collection is returned without being modified unless necessary. E.g., inserting a value into
  an ``IntSet`` should return the *same* ``IntSet`` instance when the set already contains that value.


General
=======

Lazy
----
- ``toAsync : lazyValue:Lazy<'T> -> Async<'T>``
  Adapts a lazily-initialized value to the ``async`` workflow.

- ``ofAsync : value:Async<'T> -> Lazy<'T>``
  Converts an asynchronously-evaluated value (created with the ``async`` workflow) into a lazily-initialized value.


Nullable
--------
- Implement a ``Nullable`` module similar to the ``Option`` module in the F# Core Library.
  This'll make it easier to write functional-style code for nullable values.

- Maybe add a type alias for ``IQueryable<'T>``? E.g., ``query<'T>``.


TextWriter
----------
- Add extension methods / overloads of Write and WriteLine which accept a ``substring`` value.
- Add extension methods / overloads of Write and WriteLine which accept a ``vector<char>`` value.

Control
-------
- Some of the methods in the ``ChoiceBuilder`` type may need to be reworked; for example, the
  ``TryWith``, ``TryFinally``, and ``Using`` methods don't seem to work correctly when used
  in practice. The ``For`` method depends on ``Using`` so it'll need to be fixed too.


Strings
=======

String
------
- ``exactlyOne``
- ``foldi``
- ``foldiBack``
- ``foldi2``
- ``foldiBack2``


String.Split
------------
- ``foldBack``
- ``exists``
- ``forall``
- ``scan``
- ``scanBack``
- ``mapReduce``
- ``mapScan``
- ``get``

  Given an index, gets the substring at that index in the array of substrings created by the split operation.

  **NOTE:** These should not be implemented directly; implement the ``Substring.Split`` functions first, then these functions can simply wrap those.

- ``toList``
  
  This could simply use ``String.Split.foldBack`` (once implemented) to build the list on-the-fly
  so it doesn't need to be reversed.


Substring
---------
- ``split``
- ``splits``
- ``exactlyOne``
- ``foldi``
- ``foldiBack``
- ``foldi2``
- ``foldiBack2``
- ``toList``

- Implement a ``SubstringComparer`` class similar to ``StringComparer``.

- Implement a ``substring``-compatible version of ``Regex.Matches`` as an extension method on ``Regex``.

- Implement the IList<char> interface for substring.


Substring.Split
---------------
- ``foldBack``
- ``exists``
- ``forall``
- ``scan``
- ``scanBack``
- ``mapReduce``
- ``mapScan``
- ``get``

  Given an index, gets the substring at that index in the array of substrings created by the split operation.

- ``toList``
  
  This could simply use ``String.Split.foldBack`` (once implemented) to build the list on-the-fly
  so it doesn't need to be reversed.


Collections and Data Structures
===============================

Array
-----
- ``scan2``
- ``scanBack2``


ArrayView
---------
- ``tryFindBack``
- ``findBack``
- ``tryFindIndexBack``
- ``findIndexBack``
- ``tryFindIndexOfBack``
- ``findIndexOfBack``
- ``tryPickBack``
- ``pickBack``

- Re-implement ``ArrayView`` as a new struct type instead of an abbreviation for ``System.ArraySegment<T>``. Then, we can implement structural equality and comparison on it.


AsyncSeq
--------
- ``unfold``


Bimap
-----
- ``ofMap``
- ``toMap``
- ``tryOfMap``
- ``projectKeys``
- ``projectValues``


IntMap/IntSet/HashMap/HashSet
-----------------------------
- Implement serialization/deserialization code for parity with ``Map`` and ``Set``.
- Fix the private ``ElementString`` method implementations so they take the specific element type for that collection, or use generics, instead of boxing values and casting.
- Patricia Tries (the basis of these data structures) don't preserve the expected sorting for signed integers. This should be fixed so these collection types
  can be used as drop-in replacements for the built-in F# ``Set`` and ``Map`` types. There are two possible solutions:

  - Use the new bit-twiddling functions to flip the sign bit of any values when passing them in to one of the ``PatriciaSet32`` or ``PatriciaSet64`` functions,
    or when an ``int``/``int64`` key is returned from one of these functions.

    This adds a small amount of overhead to each function call, but it would allow us to use the same Patricia Trie implementations to handle both signed
    and unsigned key types (e.g., if we want to implement ``UIntSet`` and ``UIntMap``).

  - Implement additional functions in the ``PatriciaSet32`` and ``PatriciaSet64`` modules which perform the first step of any functions which traverse the
    trie, so the sign bit could be handled correctly when the key type is ``int`` or ``int64``. The upside of this is that there's no bit-twiddling overhead,
    so performance should be the same regardless of whether the key type is signed or not. The downside of this is that the code becomes a little more bloated
    and could be slightly error-prone since we'll have to make sure to call the special 'wrapper' function whenever we're working with signed integer keys.
    *This solution may also not work correctly for little-endian Patricia tries, which is what we're currently using in the release version.*


IntBimap / LongBimap
--------------------
- ``map``
- ``ofIntMap``
- ``tryOfIntMap``
- ``projectKeys``
- ``projectValues``


IntMap / LongMap
----------------
- ``exactlyOne``
- ``ofKeys``
- ``ofValues``
- ``ofIntKeys``

  This should work like ``ofKeys`` but with ``IntSet`` instead of ``Set``.

- ``keys``
- ``values``

  Should this return a set or a multiset?

- ``extractMin``
- ``extractMax``
- ``tryExtractMin``
- ``tryExtractMax``

- Interfaces

  - ``IReadOnlyDictionary<TKey, TValue>`` (.NET 4.5)
  - ``IReadOnlyCollection<KeyValuePair<TKey, TValue>>`` (.NET 4.5)

- Implement IntMap-based versions of our custom Map functions.


IntSet / LongSet
----------------
- ``exactlyOne``
- ``extractMin``
- ``extractMax``
- ``reduce``
- ``reduceBack``
- ``tryExtractMin``
- ``tryExtractMax``
- ``scan``
- ``scanBack``
- ``allSubsets : IntSet -> seq<IntSet>``

  Returns a sequence of all subsets of the given set.

- ``subsets : IntSet -> int -> seq<IntSet>``

  Returns a sequence which produces all subsets of the given set, which have the given size.

- ``lessThan``
- ``greaterThan``

  Given an IntSet and a value, returns the subset containing the values less than (or greater than) the value.

- Interfaces

  - ``ISet<'T>`` (.NET 4.0)
  - ``IReadOnlyCollection<'T>`` (.NET 4.5)

- Implement IntSet- and TagSet-based versions of our custom Set functions.


LazyList
--------
- ``exactlyOne : list:LazyList<'T> -> 'T``

  Extracts the only item in a single-item list. An ``ArgumentException`` is raised if the list is empty or contains more than one item.

- ``force : LazyList<'T> -> unit``

  Traverses the LazyList and forces evaluation of all cells. May not terminate.

- ``forcePartial : int -> LazyList<'T> -> unit``

  Traverses the given number of cells in the LazyList (or to the end), forcing evaluation
  of the traversed cells. May not terminate.

- ``ofSeqEager : seq<'T> -> LazyList<'T>``

  Similar to 'ofSeq', but eagerly enumerates the sequence to build a LazyList.
  This allows us to detect certain sequence types (like 'T[] and 'T list) and use optimized
  implementations, avoids the possibility of memory leaks, and avoids lazily-evaluating
  list elements when they don't really need it.

- Interfaces

  - ``ICollection``
  - ``ICollection<'T>``
  - ``IList``
  - ``IList<'T>``
  - ``IReadOnlyList<'T>`` (.NET 4.5)
  - ``IReadOnlyCollection<'T>`` (.NET 4.5)

- Implement a ``DebuggerTypeProxy``? If so, we need to figure out how to do this in a safe way.

- Investigate the optimization described in issue #3 (on Github).

- Should the ``status`` field of ``LazyList<'T>`` be marked with ``[<VolatileField>]``? It seems like when a ``Delayed`` cell is forced (evaluated)
  by multiple threads at the same time, the threads will enter the lock sequentially, and since the field is not marked as volatile, any threads
  after the first thread will only see the stale ``status`` value (``Delayed``) so the generator function will be called multiple times.

  A good way to investigate this would be to write some unit tests where we use a generator function which increments a ``ref`` cell each time it's
  called; then, we'll try to traverse the list concurrently from multiple threads, using the .NET thread pool. If the generator functions are called
  multiple times, the ref cell will not have the same count as the number of elements in the ``LazyList<'T>``.

- Think about using a more intricate locking scheme in the implementation of the ``Value`` member. If multiple threads are concurrently traversing the
  ``LazyList<'T>``, we could reduce thread contention by manually implementing the lock using ``Monitor.TryEnter(object, bool)``, ``Monitor.Wait(object)``,
  and ``Monitor.PulseAll(object)``. This would allow one thread to "force" the next list element, while the other threads called ``Wait`` to yield their
  time-slice to some other threads while the generator function is evaluating.  I (``jack-pappas``) have implemented this already, and it passes all of
  the current unit tests for ``LazyList<'T>``, but it also doubled the execution time for the unit tests. This performance regression seems to be specific
  to one or two of the unit tests; if we investigate and find these tests are pathological cases, I'll add the code to the ``LazyList<'T>`` implementation.


List
----
- ``insert : index:int -> value:'T -> list:'T list -> 'T list``

  Creates a new list by inserting the value at a given index in a list.

- ``isSorted : list:'T list -> bool (requires comparison)``

  Determines if a list is sorted (in increasing order), using the default comparison for the element type of the list.

- ``isSortedBy : projection:('T -> 'Key) -> list:'T list -> bool (requires comparison)``

  Determines if a list is sorted (in increasing order), using the given projection function to compute the sort value for each list item.

- ``isSortedWith : comparison:('T -> 'T -> int) -> list:'T list -> bool``

  Determines if a list is sorted (in increasing order), using the given comparison function to compare adjacent items in the list.

- ``splice : (index : int) -> (list1 : 'T list) -> (list2 : 'T list) -> 'T list``

  Creates a new list by "splicing" the second list into the first at the given index.

- ``update : index:int -> value:'T -> list:'T list -> 'T list``

  Creates a new list by setting the element at the specified index to a given value.


Map
---
- ``exactlyOne : map:Map<'Key, 'T> -> 'Key * 'T``

  Extracts the only binding from a ``Map`` containing a single binding. An ``ArgumentException`` is raised if the ``Map`` is empty or contains more than one binding.

- ``mapi``
- ``mapiBack``

- ``foldi``
- ``foldiBack``

- ``scan (folder : 'State -> 'T -> 'State) (state : 'State) (map : Map<'Key, 'T>) : Map<'Key, 'State>``
- ``scanBack``

  Like ``Map.fold``/``Map.foldBack``, but returns a new map which holds the intermediate result after processing each key/value pair.

- ``findOrAdd (generator : 'Key -> 'T) (key : 'Key) (map : Map<'Key, 'T>) : 'T * Map<'Key, 'T>``

  Retrieves the value associated with the specified key in the map; if the key does not exist in the map,
  the key is applied to the generator function to create a value, which is then stored in the map.
  The retrieved/created value is returned along with the (possibly) updated map.

- ``tryFindOrAdd (generator : 'Key -> 'T option) (key : 'Key) (map : Map<'Key, 'T>) : 'T option * Map<'Key, 'T>``
- ``extract (key : 'Key) (map : Map<'Key, 'T>) : 'T * Map<'Key, 'T>``
- ``tryExtract (key : 'Key) (map : Map<'Key, 'T>) : 'T option * Map<'Key, 'T>``
- ``findAndUpdate (generator : 'Key -> 'T -> 'T) (key : 'Key) (map : Map<'Key, 'T>) : 'T * Map<'Key, 'T>``

  Retrieves the value associated with the specified key in the map; if the key does not exist in the map,
  ``KeyNotFoundException`` is raised. The key and original value are applied to the generator function to
  produce a new value which is stored in the map. (OPTIMIZATION: Only update the map if the generated value
  is different than the original value.)
  The retrieved value is returned along with the (possibly) updated map.

- ``addOrUpdate (generator : 'Key -> 'T option -> 'T) (key : 'Key) (map : Map<'Key, 'T>) : Map<'Key, 'T>``
- ``maxKey : (map : Map<'Key, 'T>) : 'Key``
- ``minKey : (map : Map<'Key, 'T>) : 'Key``

  The minimum/maximum key value in the map.

- ``maxKeyBy (projection : 'Key -> 'T -> 'U) (map : Map<'Key, 'T>) : 'Key (where 'U : comparison)``
- ``minKeyBy (projection : 'Key -> 'T -> 'U) (map : Map<'Key, 'T>) : 'Key (where 'U : comparison)``

  The minimum/maximum key value in the map, compared using the given function.

- ``findKeyBack``
- ``pickBack``
- ``tryPickBack``
- ``tryFindKeyBack``

  Just like the built-in functions (e.g., ``findKey``, ``pick``) except they traverse "backwards" over the map,
  i.e., from greatest to least key value. This is useful when the map could contain multiple matching
  key/value pairs and we want to choose the one with the greatest key value.


Queue
-----
- ``exactlyOne``
- ``ofList``
- ``ofArray``
- ``ofSeq``
- ``toSeq``
- ``peek``

- Interfaces

  - ``IEnumerable``
  - ``IEnumerable<'T>``
  - ``ICollection``
  - ``ICollection<'T>``
  - ``IList``
  - ``IList<'T>``
  - ``IReadOnlyList<'T>`` (.NET 4.5)

- Implement a ``DebuggerTypeProxy``, if feasible.


ResizeArray
-----------
- ``exactlyOne``
- ``ofVector``
- ``toVector``


Seq
---
- ``choosei``

  Indexed version of ``Seq.choose``.

- ``foldGroupBy``
- ``reduceGroupBy``

  Similar to ``Seq.groupBy``, but instead of building up the group sequence associated with each key,
  perform ``fold`` or ``reduce`` operations over each of the group sequences on-the-fly. This allows
  certain operations to be implemented more efficiently, because only the accumulator value is stored
  for each key, rather than the entire grouped sequence.


Set
---
- ``exactlyOne``

- ``scan : folder:('State -> 'T -> 'State) -> state:'State -> set:Set<'T> -> Set<'State>``
- ``scanBack``

- ``allSubsets : Set<'T> -> seq<Set<'T>>``

  Returns a sequence of all subsets of the given set.

- ``subsets : Set<'T> -> int -> seq<Set<'T>>``

  Returns a sequence which produces all subsets of the given set, which have the given size.

- ``lessThan``
- ``greaterThan``

  Given a ``Set`` and a value, returns the subset containing the values less than (or greater than) the value.

- ``findBack``
- ``pickBack``
- ``tryFindBack``
- ``tryPickBack``

  Just like the built-in functions (e.g., ``findKey``, ``pick``) except they traverse "backwards" over the set,
  i.e., from greatest to least value. This is useful when the set could contain multiple matching
  values and we want to choose the greatest one.

- Define a type extension for ``Set<'T>`` which provides the xor ``(^^^)`` operator,
  via the ``Set.symmetricDifference`` function.


TagBimap
--------
- ``map``
- ``ofTagMap``
- ``tryOfTagMap``
- ``projectKeys``
- ``projectValues``


Vector
------
- ``exactlyOne``
- ``findBack``
- ``pickBack``
- ``tryFindBack``
- ``tryPickBack``

  Just like the built-in functions (e.g., ``findKey``, ``pick``) except they traverse "backwards" over the vector,
  i.e., from highest to lowest index. This is useful when the vector could contain multiple matching
  values and we want to choose the one with the greatest index.

- Interfaces

  - ``IEquatable``
  - ``IEquatable<'T>``
  - ``IComparable``
  - ``IComparable<'T>``
  - ``ICollection``
  - ``ICollection<'T>``
  - ``IList``
  - ``IList<'T>``
  - ``ICloneable``
  - ``IStructuralComparable``
  - ``IStructuralEquatable``


Caching
=======

LruCache
--------
- ``findKey``
- ``tryFindKey``

  These should work like the functions in the ``Map`` module.

- ``findKeyBack``
- ``pickBack``
- ``tryPickBack``
- ``tryFindKeyBack``

  Just like the built-in functions (e.g., ``findKey``, ``pick``) except they traverse "backwards" over the cache,
  i.e., from newest (most-recently-used) to oldest (least-recently-used) key value. This is useful when the
  cache could contain multiple matching key/value pairs and we want to choose the one with the newest key value.

- Import the MapType and MapModule tests from the F# distribution and adapt them for ``LruCache``.
- Implement a comparison method similar to how LruCache.Equals is implemented.


Workflow Collections
====================

State.Array
-----------
- ``mapReduce``


State.List
----------
- ``foldi``
- ``foldBack``
- ``foldiBack``


Parallel
========
Implement parallel versions of functions similar to those in the "top-level" module
(e.g., ``Array``, ``List``), based on the TPL and/or PLINQ. These will complement the
modules which ship within the F# Core Library, providing additional functionality.

Array.Parallel
--------------
*TODO*


IntMap.Parallel
---------------
*TODO*


IntSet.Parallel
---------------
*TODO*


List.Parallel
-------------
*TODO*


Map.Parallel
------------
*TODO*


Seq.Parallel
------------
*TODO*


Set.Parallel
------------
*TODO*


Vector.Parallel
---------------
*TODO*
