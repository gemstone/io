//******************************************************************************************************
//  FileBackedDictionaryTest.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  12/03/2014 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Gemstone.ArrayExtensions;
using Gemstone.IO.Collections;
using Gemstone.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Random = Gemstone.Security.Cryptography.Random;

namespace Gemstone.IO.UnitTests.Collections;

/// <summary>
/// Example class for manual instance serialization.
/// </summary>
public class InstanceTest
{
    public InstanceTest()
    {
    }

    public InstanceTest(Guid id, string name, ConnectionState status)
    {
        ID = id;
        Name = name;
        Status = status;
    }

    public Guid ID { get; set; }

    public string Name { get; set; } = "";

    public ConnectionState Status { get; set; }

    /// <summary>
    /// Deserializes the <see cref="InstanceTest"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    public void ReadFrom(Stream stream)
    {
        BinaryReader reader = new(stream, Encoding.UTF8, true);

        ID = new Guid(reader.ReadBytes(16));
        Name = reader.ReadString();
        Status = (ConnectionState)reader.ReadByte();
    }

    /// <summary>
    /// Serializes the <see cref="InstanceTest"/> to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    public void WriteTo(Stream stream)
    {
        BinaryWriter writer = new(stream, Encoding.UTF8, true);

        writer.Write(ID.ToByteArray());
        writer.Write(Name);
        writer.Write((byte)Status);
    }
}

/// <summary>
/// Example class for manual static serialization.
/// </summary>
public class StaticTest
{
    public StaticTest()
    {
    }

    public StaticTest(Guid id, string name, ConnectionState status)
    {
        ID = id;
        Name = name;
        Status = status;
    }

    public Guid ID { get; set; }

    public string Name { get; set; } = "";

    public ConnectionState Status { get; set; }

    /// <summary>
    /// Deserializes the <see cref="InstanceTest"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
    public static object ReadFrom(Stream stream)
    {
        BinaryReader reader = new(stream, Encoding.UTF8, true);

        return new StaticTest
        {
            ID = new Guid(reader.ReadBytes(16)),
            Name = reader.ReadString(),
            Status = (ConnectionState)reader.ReadByte()
        };
    }

    /// <summary>
    /// Serializes the <see cref="InstanceTest"/> to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    /// <param name="obj">Instance to serialize.</param>
    public static void WriteTo(Stream stream, object obj)
    {
        BinaryWriter writer = new(stream, Encoding.UTF8, true);

        if (obj is not StaticTest instance)
            throw new ArgumentException($"Object is not a '{nameof(StaticTest)}'", nameof(obj));

        writer.Write(instance.ID.ToByteArray());
        writer.Write(instance.Name);
        writer.Write((byte)instance.Status);
    }
}

[TestClass]
public class FileBackedDictionaryTest
{
    [TestMethod]
    public void AddTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        dictionary.Add(0, 0);
        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 1);
    }

    [TestMethod]
    public void RemoveTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        dictionary.Add(0, 0);
        Assert.IsTrue(dictionary.ContainsKey(0));
        dictionary.Remove(0);
        Assert.IsFalse(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 0);
    }

    [TestMethod]
    public void TryGetValueTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        dictionary.Add(0, 0);
        Assert.IsTrue(dictionary.TryGetValue(0, out int value));
        Assert.AreEqual(value, 0);
    }

    [TestMethod]
    public void ClearTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        for (int i = 0; i < 100; i++)
            dictionary.Add(i, i);

        Assert.AreEqual(dictionary.Count, 100);
        dictionary.Clear();
        Assert.AreEqual(dictionary.Count, 0);
    }

    [TestMethod]
    public void CopyToTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        for (int i = 1; i <= 100; i++)
            dictionary.Add(i, i);

        Assert.AreEqual(dictionary.Count, 100);

        KeyValuePair<int, int>[] array = new KeyValuePair<int, int>[dictionary.Count];

        dictionary.CopyTo(array, 0);

        foreach (KeyValuePair<int, int> kvp in array)
        {
            Assert.IsTrue(dictionary.Contains(kvp), kvp.Key.ToString());
            Assert.AreEqual(dictionary[kvp.Key], kvp.Value);
        }
    }

    [TestMethod]
    public void CompactTest()
    {
        using FileBackedDictionary<int, int> dictionary = [];

        for (int i = 0; i < 10000; i += 4)
        {
            dictionary.Add(i, 4);

            if (i % 400 == 0)
                dictionary[i] = 400;
            else if (i % 100 == 0)
                dictionary.Remove(i);
        }

        dictionary.Compact();

        for (int i = 0; i < 10000; i++)
        {
            if (i % 400 == 0)
                Assert.AreEqual(dictionary[i], 400);
            else if (i % 100 == 0)
                Assert.IsFalse(dictionary.ContainsKey(i), i.ToString());
            else if (i % 4 == 0)
                Assert.AreEqual(dictionary[i], 4);
            else
                Assert.IsFalse(dictionary.ContainsKey(i), i.ToString());
        }
    }

    [TestMethod]
    public void StringKeyTest()
    {
        using FileBackedDictionary<string, int> dictionaryCaseInsensitive = new(StringComparer.OrdinalIgnoreCase);

        dictionaryCaseInsensitive.Add("Jake", 0);
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey("JAKE"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);

        dictionaryCaseInsensitive.Remove("jake");
        Assert.IsFalse(dictionaryCaseInsensitive.ContainsKey("0"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 0);

        dictionaryCaseInsensitive.Add("ROBERTO", 0);
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey("roberto"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);

        using FileBackedDictionary<string, int> dictionaryCaseSensitive = new();

        dictionaryCaseSensitive.Add("Jake", 0);
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey("Jake"));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("JAKE"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);

        dictionaryCaseSensitive.Remove("Jake");
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("Jake"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 0);

        dictionaryCaseSensitive.Add("ROBERTO", 0);
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey("ROBERTO"));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("Roberto"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);
    }

    [TestMethod]
    public void StringStringTest()
    {
        using FileBackedDictionary<string, string> dictionaryCaseInsensitive = new(StringComparer.OrdinalIgnoreCase);

        dictionaryCaseInsensitive.Add("Jake", "value0");
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey("JAKE"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);
        Assert.AreEqual(dictionaryCaseInsensitive["jake"], "value0");

        dictionaryCaseInsensitive.Remove("jake");
        Assert.IsFalse(dictionaryCaseInsensitive.ContainsKey("value0"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 0);

        dictionaryCaseInsensitive.Add("ROBERTO", "value0");
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey("roberto"));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);
        Assert.AreEqual(dictionaryCaseInsensitive["roberto"], "value0");

        using FileBackedDictionary<string, string> dictionaryCaseSensitive = new();

        dictionaryCaseSensitive.Add("Jake", "value0");
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey("Jake"));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("JAKE"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);
        Assert.AreEqual(dictionaryCaseSensitive["Jake"], "value0");

        dictionaryCaseSensitive.Remove("Jake");
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("Jake"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 0);

        dictionaryCaseSensitive.Add("ROBERTO", "value0");
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey("ROBERTO"));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey("Roberto"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);
        Assert.AreEqual(dictionaryCaseSensitive["ROBERTO"], "value0");

        // Test null values
        dictionaryCaseSensitive.Add("Jake", null);
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey("Jake"));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 2);
        Assert.IsTrue(dictionaryCaseSensitive["Jake"] is null);

        dictionaryCaseSensitive["Jake"] = "value1";
        Assert.AreEqual(dictionaryCaseSensitive["Jake"], "value1");
    }

    private class StringArrayComparer : IEqualityComparer<string[]>
    {
        private readonly bool m_ignoreCase;

        public StringArrayComparer(bool ignoreCase)
        {
            m_ignoreCase = ignoreCase;
        }

        public bool Equals(string[] x, string[] y)
        {
            return x?.SequenceEqual(y ?? [],
                m_ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) ?? false;
        }

        public int GetHashCode(string[] obj)
        {
            return m_ignoreCase
                ? obj.Aggregate(0, (hash, item) => hash ^ item.ToUpperInvariant().GetHashCode())
                : obj.Aggregate(0, (hash, item) => hash ^ item.GetHashCode());
        }
    }

    [TestMethod]
    public void StringArrayKeyTest()
    {
        using FileBackedDictionary<string[], int> dictionaryCaseInsensitive = new(new StringArrayComparer(true));

        dictionaryCaseInsensitive.Add(["Jake"], 0);
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey(["JAKE"]));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);

        dictionaryCaseInsensitive.Remove(["jake"]);
        Assert.IsFalse(dictionaryCaseInsensitive.ContainsKey(["0"]));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 0);

        dictionaryCaseInsensitive.Add(["ROBERTO"], 0);
        Assert.IsTrue(dictionaryCaseInsensitive.ContainsKey(["roberto"]));
        Assert.AreEqual(dictionaryCaseInsensitive.Count, 1);

        using FileBackedDictionary<string[], int> dictionaryCaseSensitive = new(new StringArrayComparer(false));

        dictionaryCaseSensitive.Add(["Jake"], 0);
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey(["Jake"]));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey(["JAKE"]));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);

        dictionaryCaseSensitive.Remove(["Jake"]);
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey(["Jake"]));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 0);

        dictionaryCaseSensitive.Add(["ROBERTO"], 0);
        Assert.IsTrue(dictionaryCaseSensitive.ContainsKey(["ROBERTO"]));
        Assert.IsFalse(dictionaryCaseSensitive.ContainsKey(["Roberto"]));
        Assert.AreEqual(dictionaryCaseSensitive.Count, 1);

        // Test case-insensitive comparison with string key array with two elements
        using FileBackedDictionary<string[], int> dictionaryCaseInsensitive2 = new(new StringArrayComparer(true));

        dictionaryCaseInsensitive2.Add(["Jake", "Smith"], 0);
        Assert.IsTrue(dictionaryCaseInsensitive2.ContainsKey(["JAKE", "SMITH"]));
        Assert.AreEqual(dictionaryCaseInsensitive2.Count, 1);

        dictionaryCaseInsensitive2.Remove(["jake", "smith"]);
        Assert.IsFalse(dictionaryCaseInsensitive2.ContainsKey(["0", "1"]));
        Assert.AreEqual(dictionaryCaseInsensitive2.Count, 0);

        dictionaryCaseInsensitive2.Add(["ROBERTO", "GARCIA"], 0);
        Assert.IsTrue(dictionaryCaseInsensitive2.ContainsKey(["roberto", "garcia"]));
        Assert.AreEqual(dictionaryCaseInsensitive2.Count, 1);
    }

    [TestMethod]
    public void IntArrayTest()
    {
        ArrayTest(count =>
        {
            int[] array = new int[count];

            for (int index = 0; index < count; index++)
                array[index] = index;

            return array;
        });
    }

    [TestMethod]
    public void StringArrayTest()
    {
        PasswordGenerator generator = new();

        ArrayTest(count =>
        {
            string[] array = new string[count];

            for (int index = 0; index < count; index++)
                array[index] = generator.GeneratePassword(Random.Int32Between(5, 20));

            return array;
        });
    }

    [TestMethod]
    public void DoubleArrayTest()
    {
        ArrayTest(count =>
        {
            double[] array = new double[count];

            for (int index = 0; index < count; index++)
                array[index] = Random.Int32Between(-9999999, 9999999) * Random.Number;

            return array;
        });
    }

    [TestMethod]
    public void DateTimeArrayTest()
    {
        ArrayTest(count =>
        {
            DateTime[] array = new DateTime[count];

            for (int index = 0; index < count; index++)
                array[index] = new DateTime(Random.Int64Between(0, DateTime.MaxValue.Ticks));

            return array;
        });
    }

    private void ArrayTest<T>(Func<int, T[]> indexer) where T : IComparable<T>
    {
        using FileBackedDictionary<int, T[]> dictionary = [];

        T[] array = indexer(2);
        dictionary.Add(0, array);
        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 1);
        Assert.IsTrue(dictionary[0].CompareTo(array) == 0);

        array = indexer(3);
        dictionary.Add(1, array);
        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[1].CompareTo(array) == 0);

        array = indexer(5);
        dictionary[0] = array;
        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[0].CompareTo(array) == 0);

        array = indexer(4);
        dictionary[1] = array;
        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[1].CompareTo(array) == 0);
    }

    [TestMethod]
    public void IntListTest()
    {
        ListTest(count =>
        {
            List<int> list = [];

            for (int index = 0; index < count; index++)
                list.Add(index);

            return list;
        });
    }

    [TestMethod]
    public void StringListTest()
    {
        PasswordGenerator generator = new();

        ListTest(count =>
        {
            List<string> list = [];

            for (int index = 0; index < count; index++)
                list.Add(generator.GeneratePassword(Random.Int32Between(5, 20)));

            return list;
        });
    }

    [TestMethod]
    public void DoubleListTest()
    {
        ListTest(count =>
        {
            List<double> list = [];

            for (int index = 0; index < count; index++)
                list.Add(Random.Int32Between(-9999999, 9999999) * Random.Number);

            return list;
        });
    }

    [TestMethod]
    public void DateTimeListTest()
    {
        ListTest(count =>
        {
            List<DateTime> list = [];

            for (int index = 0; index < count; index++)
                list.Add(new DateTime(Random.Int64Between(0, DateTime.MaxValue.Ticks)));

            return list;
        });
    }

    private void ListTest<T>(Func<int, List<T>> indexer) where T : IComparable<T>
    {
        using FileBackedDictionary<int, List<T>> dictionary = [];

        List<T> list = indexer(2);
        dictionary.Add(0, list);
        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 1);
        Assert.IsTrue(dictionary[0].ToArray().CompareTo(list.ToArray()) == 0);

        list = indexer(3);
        dictionary.Add(1, list);
        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[1].ToArray().CompareTo(list.ToArray()) == 0);

        list = indexer(5);
        dictionary[0] = list;
        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[0].ToArray().CompareTo(list.ToArray()) == 0);

        list = indexer(4);
        dictionary[1] = list;
        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[1].ToArray().CompareTo(list.ToArray()) == 0);
    }

    [TestMethod]
    public void InstanceSerializationTest()
    {
        using FileBackedDictionary<int, InstanceTest[]> dictionary = [];

        dictionary.Add(0,
        [
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test1.1", Status = ConnectionState.Open },
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test1.2", Status = ConnectionState.Closed }
        ]);

        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 1);
        Assert.IsTrue(dictionary[0][0].Name == "Test1.1");
        Assert.IsTrue(dictionary[0][1].Status == ConnectionState.Closed);

        dictionary.Add(1,
        [
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test2.1", Status = ConnectionState.Open },
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test2.2", Status = ConnectionState.Closed },
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test2.3", Status = ConnectionState.Executing },
            new InstanceTest { ID = Guid.NewGuid(), Name = "Test2.4", Status = ConnectionState.Broken }
        ]);

        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[0][1].Name == "Test1.2");
        Assert.IsTrue(dictionary[1][2].Name == "Test2.3");
        Assert.IsTrue(dictionary[1][3].Status == ConnectionState.Broken);
    }

    [TestMethod]
    public void StaticSerializationTest()
    {
        using FileBackedDictionary<int, StaticTest[]> dictionary = [];

        dictionary.Add(0,
        [
            new StaticTest { ID = Guid.NewGuid(), Name = "Test1.1", Status = ConnectionState.Open },
            new StaticTest { ID = Guid.NewGuid(), Name = "Test1.2", Status = ConnectionState.Closed }
        ]);

        Assert.IsTrue(dictionary.ContainsKey(0));
        Assert.AreEqual(dictionary.Count, 1);
        Assert.IsTrue(dictionary[0][0].Name == "Test1.1");
        Assert.IsTrue(dictionary[0][1].Status == ConnectionState.Closed);

        dictionary.Add(1,
        [
            new StaticTest { ID = Guid.NewGuid(), Name = "Test2.1", Status = ConnectionState.Open },
            new StaticTest { ID = Guid.NewGuid(), Name = "Test2.2", Status = ConnectionState.Closed },
            new StaticTest { ID = Guid.NewGuid(), Name = "Test2.3", Status = ConnectionState.Executing },
            new StaticTest { ID = Guid.NewGuid(), Name = "Test2.4", Status = ConnectionState.Broken }
        ]);

        Assert.IsTrue(dictionary.ContainsKey(1));
        Assert.AreEqual(dictionary.Count, 2);
        Assert.IsTrue(dictionary[0][1].Name == "Test1.2");
        Assert.IsTrue(dictionary[1][2].Name == "Test2.3");
        Assert.IsTrue(dictionary[1][3].Status == ConnectionState.Broken);
    }
}
