//******************************************************************************************************
//  InstanceTestWithIList.cs - Gbtc
//
//  Copyright © 2024, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  08/12/2024 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections;
using System.Collections.Generic;

namespace Gemstone.IO.UnitTests.Collections;

public class InstanceTestWithIList : IList
{
    private readonly List<InstanceTest> m_list = [];

    public IEnumerator GetEnumerator()
    {
        return m_list.GetEnumerator();
    }

    public void CopyTo(Array array, int index)
    {
        ((IList)m_list).CopyTo(array, index);
    }

    public int Count => m_list.Count;

    public bool IsSynchronized => ((IList)m_list).IsSynchronized;
    
    public object SyncRoot => this;
    
    public int Add(object value)
    {
        return ((IList)m_list).Add(value);
    }

    public void Clear()
    {
        m_list.Clear();
    }

    public bool Contains(object value)
    {
        return ((IList)m_list).Contains(value);
    }

    public int IndexOf(object value)
    {
        return ((IList)m_list).IndexOf(value);
    }

    public void Insert(int index, object value)
    {
        ((IList)m_list).Insert(index, value);
    }

    public void Remove(object value)
    {
        ((IList)m_list).Remove(value);
    }

    public void RemoveAt(int index)
    {
        m_list.RemoveAt(index);
    }

    public bool IsFixedSize => ((IList)m_list).IsFixedSize;

    public bool IsReadOnly => ((IList)m_list).IsReadOnly;

    public object this[int index]
    {
        get => ((IList)m_list)[index];
        set => ((IList)m_list)[index] = value;
    }
}
