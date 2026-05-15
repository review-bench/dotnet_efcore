// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Metadata.Internal;

public class IndexTest
{
    [Fact]
    public void Throws_when_model_is_readonly()
    {
        var model = CreateModel();
        var entityType = model.AddEntityType("E");
        var property = entityType.AddProperty("P", typeof(int));
        var index = entityType.AddIndex([property]);

        model.FinalizeModel();

        Assert.Equal(
            CoreStrings.ModelReadOnly,
            Assert.Throws<InvalidOperationException>(() => entityType.AddIndex([property])).Message);

        Assert.Equal(
            CoreStrings.ModelReadOnly,
            Assert.Throws<InvalidOperationException>(() => entityType.AddIndex([property], "Name")).Message);

        Assert.Equal(
            CoreStrings.ModelReadOnly,
            Assert.Throws<InvalidOperationException>(() => entityType.RemoveIndex(index)).Message);

        Assert.Equal(
            CoreStrings.ModelReadOnly,
            Assert.Throws<InvalidOperationException>(() => index.IsUnique = false).Message);
    }

    [Fact]
    public void Gets_expected_default_values()
    {
        var entityType = ((IConventionModel)CreateModel()).AddEntityType(typeof(Customer));
        var property1 = entityType.AddProperty(Customer.IdProperty);
        var property2 = entityType.AddProperty(Customer.NameProperty);

        var index = entityType.AddIndex([property1, property2]);

        Assert.True(new[] { property1, property2 }.SequenceEqual(index.Properties));
        Assert.False(index.IsUnique);
        Assert.Equal(ConfigurationSource.Convention, index.GetConfigurationSource());
    }

    [Fact]
    public void Can_set_unique()
    {
        var entityType = CreateModel().AddEntityType(typeof(Customer));
        var property1 = entityType.AddProperty(Customer.IdProperty);
        var property2 = entityType.AddProperty(Customer.NameProperty);

        var index = entityType.AddIndex([property1, property2]);
        index.IsUnique = true;

        Assert.True(new[] { property1, property2 }.SequenceEqual(index.Properties));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void IsDescending_all_ascending_is_normalized_to_null()
    {
        var entityType = CreateModel().AddEntityType(typeof(Customer));
        var property1 = entityType.AddProperty(Customer.IdProperty);
        var property2 = entityType.AddProperty(Customer.NameProperty);

        var index = entityType.AddIndex([property1, property2]);
        index.IsDescending = [false, false];

        Assert.True(new[] { property1, property2 }.SequenceEqual(index.Properties));
        Assert.Null(index.IsDescending);
    }

    [Fact]
    public void IsDescending_all_descending_is_normalized_to_empty()
    {
        var entityType = CreateModel().AddEntityType(typeof(Customer));
        var property1 = entityType.AddProperty(Customer.IdProperty);
        var property2 = entityType.AddProperty(Customer.NameProperty);

        var index = entityType.AddIndex([property1, property2]);
        index.IsDescending = [true, true];

        Assert.True(new[] { property1, property2 }.SequenceEqual(index.Properties));
        Assert.Equal([], index.IsDescending);
    }

    [Fact]
    public void IsDescending_invalid_number_of_columns_throws()
    {
        var entityType = CreateModel().AddEntityType(typeof(Customer));
        var property1 = entityType.AddProperty(Customer.IdProperty);
        var property2 = entityType.AddProperty(Customer.NameProperty);

        var index = entityType.AddIndex([property1, property2]);
        var exception = Assert.Throws<ArgumentException>(() => index.IsDescending = [true]);
        Assert.Equal(
            CoreStrings.InvalidNumberOfIndexSortOrderValues("{'Id', 'Name'}", 1, 2) + " (Parameter 'descending')",
            exception.Message);
    }

    private static IMutableModel CreateModel()
        => new Model();

    private class Customer
    {
        public static readonly PropertyInfo IdProperty = typeof(Customer).GetProperty("Id");
        public static readonly PropertyInfo NameProperty = typeof(Customer).GetProperty("Name");

        public int Id { get; set; }
        public string Name { get; set; }
    }

    private class Order
    {
        public static readonly PropertyInfo IdProperty = typeof(Order).GetProperty("Id");

        public int Id { get; set; }
    }

    private sealed class Blog
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public List<Post> Posts { get; set; } = [];
        public Address Owner { get; set; }
    }

    private sealed class Post
    {
        public string Title { get; set; }
        public int Rating { get; set; }
    }

    private sealed class Address
    {
        public string City { get; set; }
        public string Country { get; set; }
    }

    private static ModelBuilder CreateComplexModelBuilder()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Blog>(b =>
        {
            b.Property(e => e.Title);

            b.ComplexProperty(e => e.Owner, cb =>
            {
                cb.Property(a => a.City);
                cb.Property(a => a.Country);
            });

            b.ComplexCollection(e => e.Posts, cb =>
            {
                cb.Property(p => p.Title);
                cb.Property(p => p.Rating);
            });
        });

        return modelBuilder;
    }

    [Theory]
    [InlineData("Posts[")]                  // unterminated bracket
    [InlineData("Posts[abc].Title")]        // non-numeric index
    [InlineData("Posts[-1].Title")]         // negative index
    [InlineData("Posts[].")]                // empty trailing segment
    [InlineData(".Title")]                  // empty leading segment
    [InlineData("[0].Title")]               // bracket at start of segment
    [InlineData("")]                        // empty path
    public void MatchComplexPath_rejects_invalid_path(string path)
    {
        Assert.Null(InternalTypeBaseBuilder.MatchComplexPath(path));
    }

    [Theory]
    [InlineData("Posts[].Title")]
    [InlineData("Posts[*].Title")]
    public void MatchComplexPath_accepts_all_elements_syntaxes(string path)
    {
        var parsed = InternalTypeBaseBuilder.MatchComplexPath(path);
        Assert.NotNull(parsed);
        Assert.Equal(["Posts", "Title"], parsed.Value.MemberNames);
        Assert.Equal([true, false], parsed.Value.IsCollection);
        Assert.Equal([null], parsed.Value.CollectionIndices);
    }

    [Fact]
    public void MatchComplexPath_preserves_collection_flag_on_leaf()
    {
        var parsed = InternalTypeBaseBuilder.MatchComplexPath("Posts[]");
        Assert.NotNull(parsed);
        Assert.Equal(["Posts"], parsed.Value.MemberNames);
        Assert.Equal([true], parsed.Value.IsCollection);
        Assert.Equal([null], parsed.Value.CollectionIndices);
    }

    [Fact]
    public void MatchComplexPath_preserves_indexer_on_leaf()
    {
        var parsed = InternalTypeBaseBuilder.MatchComplexPath("Posts[3]");
        Assert.NotNull(parsed);
        Assert.Equal(["Posts"], parsed.Value.MemberNames);
        Assert.Equal([true], parsed.Value.IsCollection);
        Assert.Equal([3], parsed.Value.CollectionIndices);
    }

    [Fact]
    public void MatchComplexPath_single_scalar_member_emits_single_flag()
    {
        var parsed = InternalTypeBaseBuilder.MatchComplexPath("Title");
        Assert.NotNull(parsed);
        Assert.Equal(["Title"], parsed.Value.MemberNames);
        Assert.Equal([false], parsed.Value.IsCollection);
        Assert.Null(parsed.Value.CollectionIndices);
    }

    [Fact]
    public void FindIndex_with_collection_indices_returns_matching_json_path_index()
    {
        // (Properties, CollectionIndices) form the full identity of an unnamed JSON-path index.
        // FindIndex(properties, CI) must locate it; the no-CI overload should not, because that overload
        // looks up the "plain" (CI=null) index identity.
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Entity<Blog>().Metadata;
        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;
        entityType.AddIndex(
            [titleProp],
            [[0]],
            ConfigurationSource.Explicit);

        var found = entityType.FindIndex([titleProp], [[0]]);
        Assert.NotNull(found);
        Assert.Equal([0], Assert.Single(found.CollectionIndices!));

        Assert.Null(entityType.FindIndex([titleProp], [[null]]));
        Assert.Null(entityType.FindIndex([titleProp]));
    }

    [Fact]
    public void FindIndex_without_collection_indices_returns_plain_index_only()
    {
        // When both a plain index and a JSON-path index exist over the same leaf, FindIndex(properties)
        // should return the plain (CI=null) one — JSON-path indexes are addressable only via the
        // (properties, CI) overload.
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Entity<Blog>().Metadata;
        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;
        entityType.AddIndex(
            [titleProp],
            [[0]],
            ConfigurationSource.Explicit);
        entityType.AddIndex([titleProp], ConfigurationSource.Explicit);

        var foundPlain = entityType.FindIndex([titleProp]);
        Assert.NotNull(foundPlain);
        Assert.Null(foundPlain.CollectionIndices);

        var foundJson = entityType.FindIndex(
            [titleProp], [[0]]);
        Assert.NotNull(foundJson);
        Assert.Equal([0], Assert.Single(foundJson.CollectionIndices!));
    }

    [Fact]
    public void AddIndex_unnamed_with_different_collection_indices_does_not_throw_duplicate()
    {
        // Adding two unnamed indexes with the same Properties but different CollectionIndices via the
        // internal AddIndex API succeeds because their UnnamedIndexKey identities differ.
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Entity<Blog>().Metadata;
        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;

        entityType.AddIndex(
            [titleProp],
            [[0]],
            ConfigurationSource.Explicit);

        entityType.AddIndex(
            [titleProp],
            [[1]],
            ConfigurationSource.Explicit);

        Assert.Equal(2, entityType.GetIndexes().Count());
    }

    [Fact]
    public void NormalizeCollectionIndices_throws_when_entry_length_exceeds_collection_count()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Model.FindEntityType(typeof(Blog))!;

        // Posts is a single complex collection, so the entry for a leaf inside it should have exactly 1 element.
        // Providing 2 elements should throw.
        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;
        var tooManyIndices = new IReadOnlyList<int?>[] { [null, null] };

        var ex = Assert.Throws<ArgumentException>(
            () => new Index(
                [titleProp], tooManyIndices, entityType, ConfigurationSource.Explicit));

        Assert.Contains(CoreStrings.InvalidCollectionIndicesEntryLength("Title", "{'" + titleProp.Name + "'}", 2, 1), ex.Message);
    }

    [Fact]
    public void NormalizeCollectionIndices_throws_when_entry_length_is_zero_for_collection_property()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Model.FindEntityType(typeof(Blog))!;

        // Posts is a single complex collection, so providing an empty entry (0 elements) should throw.
        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;
        var emptyIndices = new IReadOnlyList<int?>[] { Array.Empty<int?>() };

        var ex = Assert.Throws<ArgumentException>(
            () => new Index(
                [titleProp], emptyIndices, entityType, ConfigurationSource.Explicit));

        Assert.Contains(CoreStrings.InvalidCollectionIndicesEntryLength("Title", "{'" + titleProp.Name + "'}", 0, 1), ex.Message);
    }

    [Fact]
    public void NormalizeCollectionIndices_throws_when_non_null_entry_for_non_collection_property()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Model.FindEntityType(typeof(Blog))!;

        // Owner.City is NOT inside a complex collection, so the entry should be null (0 collection segments).
        // Providing a non-null entry with 1 element should throw.
        var cityProp = (PropertyBase)entityType.FindComplexProperty("Owner")!.ComplexType.FindProperty("City")!;
        var wrongIndices = new IReadOnlyList<int?>[] { [null] };

        var ex = Assert.Throws<ArgumentException>(
            () => new Index(
                [cityProp], wrongIndices, entityType, ConfigurationSource.Explicit));

        Assert.Contains(CoreStrings.InvalidCollectionIndicesEntryLength("City", "{'" + cityProp.Name + "'}", 1, 0), ex.Message);
    }

    [Fact]
    public void NormalizeCollectionIndices_accepts_correct_entry_length_for_collection_property()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityType = (EntityType)modelBuilder.Model.FindEntityType(typeof(Blog))!;

        var titleProp = (PropertyBase)entityType.FindComplexProperty("Posts")!.ComplexType.FindProperty("Title")!;
        var correctIndices = new IReadOnlyList<int?>[] { [null] };

        var index = new Index(
            [titleProp], correctIndices, entityType, ConfigurationSource.Explicit);

        Assert.NotNull(index.CollectionIndices);
        Assert.Equal([null], Assert.Single(index.CollectionIndices));
    }

    [Fact]
    public void GetOrCreateProperties_returns_existing_complex_property_as_leaf_for_string_path()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityTypeBuilder = ((EntityType)modelBuilder.Entity<Blog>().Metadata).Builder;

        var properties = entityTypeBuilder.GetOrCreateProperties(
            [["Owner"]],
            isCollection: null,
            ConfigurationSource.Explicit);

        Assert.NotNull(properties);
        var leaf = Assert.Single(properties);
        Assert.IsType<ComplexProperty>(leaf);
        Assert.Equal("Owner", leaf.Name);
    }

    [Fact]
    public void GetOrCreateProperties_returns_existing_complex_collection_as_leaf_for_string_path()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityTypeBuilder = ((EntityType)modelBuilder.Entity<Blog>().Metadata).Builder;

        var properties = entityTypeBuilder.GetOrCreateProperties(
            [["Posts"]],
            isCollection: null,
            ConfigurationSource.Explicit);

        Assert.NotNull(properties);
        var leaf = Assert.Single(properties);
        var leafComplex = Assert.IsType<ComplexProperty>(leaf);
        Assert.True(leafComplex.IsCollection);
        Assert.Equal("Posts", leaf.Name);
    }

    [Fact]
    public void GetOrCreateProperties_returns_existing_complex_property_as_leaf_for_member_chain()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityTypeBuilder = ((EntityType)modelBuilder.Entity<Blog>().Metadata).Builder;
        var ownerMember = typeof(Blog).GetProperty(nameof(Blog.Owner))!;

        var properties = entityTypeBuilder.GetOrCreateProperties(
            [[ownerMember]],
            isCollection: null,
            ConfigurationSource.Explicit);

        Assert.NotNull(properties);
        var leaf = Assert.Single(properties);
        Assert.IsType<ComplexProperty>(leaf);
        Assert.Equal("Owner", leaf.Name);
    }

    [Fact]
    public void GetOrCreateProperties_returns_existing_complex_collection_as_leaf_for_member_chain()
    {
        var modelBuilder = CreateComplexModelBuilder();
        var entityTypeBuilder = ((EntityType)modelBuilder.Entity<Blog>().Metadata).Builder;
        var postsMember = typeof(Blog).GetProperty(nameof(Blog.Posts))!;

        var properties = entityTypeBuilder.GetOrCreateProperties(
            [[postsMember]],
            isCollection: null,
            ConfigurationSource.Explicit);

        Assert.NotNull(properties);
        var leaf = Assert.Single(properties);
        var leafComplex = Assert.IsType<ComplexProperty>(leaf);
        Assert.True(leafComplex.IsCollection);
        Assert.Equal("Posts", leaf.Name);
    }
}
