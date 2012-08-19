using System;
using System.Linq;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Collections.Generic;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class ImportAttributeTest 
    {
        #region Components

        [Export(Constants.ContractName, typeof(Person))]
        private sealed class Person { } 

        private interface ILocation { }

        [Export(Constants.ContractName, typeof(ILocation))]
        private sealed class Location1 : ILocation { } 

        [Export(Constants.ContractName, typeof(ILocation))]
        private sealed class Location2 : ILocation { } 

        #endregion

        #region Imports

        [Export(Constants.ContractName, typeof(FieldType))]
        private sealed class FieldType
        {
#pragma warning disable 649

            [EditorUtilsImport]
            internal Person Person;

            [EditorUtilsImportMany]
            internal IEnumerable<ILocation> List;

#pragma warning restore 649
        }

        [Export(Constants.ContractName, typeof(ConstructorType))]
        private sealed class ConstructorType 
        {
            internal readonly Person Person;
            internal readonly IEnumerable<ILocation> List;

            [ImportingConstructor]
            public ConstructorType(
                [EditorUtilsImport] Person person,
                [EditorUtilsImportMany] IEnumerable<ILocation> list)
            {
                Person = person;
                List = list;
            }
        }

        #endregion

        [ThreadStatic]
        private static CompositionContainer ImportCompositionContainer;

        public ImportAttributeTest()
        {
            if (ImportCompositionContainer == null)
            {
                ImportCompositionContainer = CreateCompositionContainer();
            }
        }

        private static CompositionContainer CreateCompositionContainer()
        {
            var list = new List<ComposablePartCatalog>();
            list.Add(new TypeCatalog(typeof(Location1)));
            list.Add(new TypeCatalog(typeof(Location2)));
            list.Add(new TypeCatalog(typeof(Person)));
            list.Add(new TypeCatalog(typeof(FieldType)));
            list.Add(new TypeCatalog(typeof(ConstructorType)));
            var catalog = new AggregateCatalog(list);
            return new CompositionContainer(catalog);
        }

        [Fact]
        public void FieldsCompose()
        {
            var type = new FieldType();
            ImportCompositionContainer.ComposeParts(type);
            Assert.NotNull(type.Person);
            Assert.Equal(2, type.List.Count());
        }

        [Fact]
        public void Fields()
        {
            var type = ImportCompositionContainer.GetExportedValue<FieldType>(Constants.ContractName);
            Assert.NotNull(type.Person);
            Assert.Equal(2, type.List.Count());
        }

        [Fact]
        public void Constructors()
        {
            var type = ImportCompositionContainer.GetExportedValue<ConstructorType>(Constants.ContractName);
            Assert.NotNull(type.Person);
            Assert.Equal(2, type.List.Count());
        }
    }
}
