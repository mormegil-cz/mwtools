// MWLib
// Copyright (c) 2007  Petr Kadlec <mormegil@centrum.cz>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace MWLib
{
    /// <summary>
    /// Simple container for two items
    /// </summary>
    /// <typeparam name="T1">Type of the first item</typeparam>
    /// <typeparam name="T2">Type of the second item</typeparam>
    public class Pair<T1, T2> : IEquatable<Pair<T1, T2>>, IComparable, IComparable<Pair<T1, T2>>
    {
        /// <summary>
        /// The first item in the pair
        /// </summary>
        public T1 First;

        /// <summary>
        /// The second item in the pair
        /// </summary>
        public T2 Second;

        /// <summary>
        /// Default constructor, the items are left empty (null, 0, false etc.)
        /// </summary>
        public Pair()
        {
        }

        /// <summary>
        /// Constructor initializing the container with the given items
        /// </summary>
        /// <param name="first">The first item to be stored</param>
        /// <param name="second">The second item to be stored</param>
        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }

        /// <summary>
        /// Computes hash code for this pair
        /// </summary>
        /// <returns>Hash code combined from hash codes of stored items</returns>
        public override int GetHashCode()
        {
            return First.GetHashCode() ^ Second.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="Object"/> is equal to the current Pair.
        /// </summary>
        /// <param name="obj">The <see cref="Object"/> to compare with the current Pair.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="obj"/> is a Pair of the same type containing items equal to
        /// the items in this Pair. <c>false</c> otherwise.
        /// </returns>
        public override bool Equals(object obj)
        {
            Pair<T1, T2> other = obj as Pair<T1, T2>;
            if (other == null) return base.Equals(obj);
            else return Equals(other);
        }

        /// <summary>
        /// Determines whether the specified Pair is equal to the current Pair.
        /// </summary>
        /// <param name="other">The Pair to compare with the current Pair.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="other"/> contains items equal to
        /// the items in this Pair. <c>false</c> otherwise.
        /// </returns>
        public bool Equals(Pair<T1, T2> other)
        {
            if (First == null)
            {
                if (other.First != null) return false;
            }
            else
            {
                if (!First.Equals(other.First)) return false;
            }
            if (Second == null)
            {
                if (other.Second != null) return false;
            }
            else
            {
                if (!Second.Equals(other.Second)) return false;
            }

            return true;
        }

        ///<summary>
        ///Compares the current instance with another object of the same type.
        ///</summary>
        ///
        ///<returns>
        ///A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance is less than obj. Zero This instance is equal to obj. Greater than zero This instance is greater than obj.
        ///</returns>
        ///
        ///<param name="obj">An object to compare with this instance. </param>
        ///<exception cref="T:System.ArgumentException">obj is not the same type as this instance. </exception><filterpriority>2</filterpriority>
        public int CompareTo(object obj)
        {
            if (obj.GetType() != this.GetType()) throw new ArgumentException("The argument must be the same type as this instance", "obj");

            return CompareTo((Pair<T1, T2>) obj);
        }

        ///<summary>
        ///Compares the current object with another object of the same type.
        ///</summary>
        ///
        ///<returns>
        ///A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the other parameter.Zero This object is equal to other. Greater than zero This object is greater than other.
        ///</returns>
        ///
        ///<param name="other">An object to compare with this object.</param>
        public int CompareTo(Pair<T1, T2> other)
        {
            if (ReferenceEquals(other, null)) return +1;

            if (ReferenceEquals(First, null))
            {
                if (!ReferenceEquals(other.First, null)) return -1;
            }
            else
            {
                IComparable first = First as IComparable;
                if (first != null)
                {
                    int result = first.CompareTo(other.First);
                    if (result != 0) return result;
                }
            }

            if (ReferenceEquals(Second, null))
            {
                if (!ReferenceEquals(other.Second, null)) return -1;
                return 0;
            }
            IComparable second = Second as IComparable;
            if (second == null) throw new NotSupportedException("This instance is not comparable with the given instance");
            return second.CompareTo(other.Second);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="pairA">First operand</param>
        /// <param name="pairB">Second operand</param>
        /// <returns><c>true</c> if both operands contain are equal, <c>false</c> otherwise</returns>
        public static bool operator == (Pair<T1, T2> pairA, Pair<T1, T2> pairB)
        {
            return !Equals(pairA, pairB);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="pairA">First operand</param>
        /// <param name="pairB">Second operand</param>
        /// <returns><c>false</c> if both operands contain are equal, <c>true</c> otherwise</returns>
        public static bool operator !=(Pair<T1, T2> pairA, Pair<T1, T2> pairB)
        {
            return !(pairA == pairB);
        }
    }
}
