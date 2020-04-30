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
using System.Collections.Generic;
using System.Globalization;
using System.Security.Permissions;
#if NUNIT
using NUnit.Framework;
#endif

[assembly: CLSCompliant(true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Execution = true)]
namespace MWLib
{
    /// <summary>
    /// Built-in namespaces
    /// </summary>
    public enum Namespace
    {
        Main = 0,
        Talk,
        User,
        User_talk,
        Project,
        Project_talk,
        Image,
        Image_talk,
        MediaWiki,
        MediaWiki_talk,
        Template,
        Template_talk,
        Help,
        Help_talk,
        Category,
        Category_talk,

        Media = -2,
        Special = -1,

        Portal = 100,
        Portal_talk
    }

    /// <summary>
    /// One MediaWiki page
    /// </summary>
    public class Page
    {
        /// <summary>
        /// Namespace the page is in
        /// </summary>
        public Namespace Namespace;

        /// <summary>
        /// Title of the page (without the namespace prefix)
        /// </summary>
        public string Title;

        /// <summary>
        /// Page identifier (in database)
        /// </summary>
        public int Id;

        public bool IsRedirect;

        /// <summary>
        /// Access restrictions for the page
        /// </summary>
        public HashSet<string> Restrictions = new HashSet<string>();

        /// <summary>
        /// Set of categories the page is in
        /// </summary>
        public HashSet<Category> Categories = new HashSet<Category>();

        /// <summary>
        /// Set of templates the page uses (transcludes)
        /// </summary>
        public HashSet<Page> Templates = new HashSet<Page>();

        /// <summary>
        /// Set of pages this page links to
        /// </summary>
        public HashSet<Page> InternalLinks = new HashSet<Page>();

        // public HashSet<Page> PagesLinkingHere = new HashSet<Page>();

        /// <summary>
        /// Set of external links contained in this page
        /// </summary>
        public HashSet<string> ExternalLinks = new HashSet<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ns">Namespace</param>
        /// <param name="title">Title (without the namespace prefix)</param>
        /// <param name="id">Page ID (in database)</param>
        /// <param name="isRedirect">Is the page a redirect?</param>
        public Page(Namespace ns, string title, int id, bool isRedirect)
        {
            if (title == null) throw new ArgumentNullException("title");

            this.Namespace = ns;
            this.Title = title;
            this.Id = id;
            this.IsRedirect = isRedirect;
        }

        /// <summary>
        /// Helper function to parse a string for a namespace
        /// </summary>
        /// <param name="value">String possibly containing a namespace name</param>
        /// <param name="namespaces">Set of all known namespace names (or <c>null</c> if only built-in namespace names should be checked)</param>
        /// <returns>Namespace, or <c>null</c> if <paramref name="value"/> does not correspond to any namespace</returns>
        public static Namespace? ParseNamespace(string value, Dictionary<int, string> namespaces)
        {
            if (value == null) throw new ArgumentNullException("value");

            object nsValue = null;

            // check given namespace names
            if (namespaces != null)
            {
                foreach (KeyValuePair<int, string> nsEntry in namespaces)
                {
                    if (String.Equals(value, nsEntry.Value, StringComparison.InvariantCultureIgnoreCase))
                    {
                        nsValue = (Namespace)nsEntry.Key;
                        break;
                    }
                }
            }
            int nsInt;
            // check if it is a plain integer
            if (Int32.TryParse(value, out nsInt))
            {
                // if the number is definitely not a namespace identifier, assume Main (for something like "2001: Space Odyssey")
                if (namespaces != null && !namespaces.ContainsKey(nsInt)) return Namespace.Main;
                // otherwise, allow "1" instead of "Talk" etc.
                return (Namespace?)nsInt;
            }
            // otherwise, try enum identifiers
            try
            {
                if (nsValue == null) nsValue = Enum.Parse(typeof(Namespace), value, true);
            }
            catch (ArgumentException)
            {
            }
            return (Namespace?)nsValue;
        }

        /// <summary>
        /// Parse page title
        /// </summary>
        /// <param name="full">Full page title (possibly including the namespace prefix)</param>
        /// <param name="namespaces">Set of all known namespace names (or <c>null</c> if only built-in namespace names are used)</param>
        /// <param name="ns">Resulting namespace</param>
        /// <param name="title">Resulting page title without the namespace prefix</param>
        public static void ParseTitle(string full, Dictionary<int, string> namespaces, out Namespace ns, out string title)
        {
            if (full == null) throw new ArgumentNullException("full");

            // remove initial colon (for cases like “:Category:Foobar”)
            if (full.StartsWith(":")) full = full.Substring(1);

            int colon = full.IndexOf(':');
            switch (colon)
            {
                case -1:
                    // no colon (or already removed initial colon): obviously main namespace
                    ns = Namespace.Main;
                    title = full;
                    return;
                case 0:
                    // should not happen, really, means something like “::Foobar”
                    ns = Namespace.Main;
                    title = full.Substring(1);
                    return;
                default:
                    // otherwise: split the namespace part and test if it is a namespace identifier
                    string nsString = full.Substring(0, colon);
                    Namespace? nsValue = ParseNamespace(nsString, namespaces);
                    if (nsValue == null)
                    {
                        // the full name contains colon, but not as a namespace prefix
                        ns = Namespace.Main;
                        title = full;
                    }
                    else
                    {
                        // the first part is a valid namespace identifier, the rest is the page name
                        ns = nsValue.Value;
                        title = full.Substring(colon + 1);
                    }
                    return;
            }
        }

        /// <summary>
        /// Parse page title (using only the built-in namespace names)
        /// </summary>
        /// <param name="full">Full page title (possibly including the namespace prefix)</param>
        /// <param name="ns">Resulting namespace</param>
        /// <param name="title">Resulting page title without the namespace prefix</param>
        public static void ParseTitle(string full, out Namespace ns, out string title)
        {
            ParseTitle(full, null, out ns, out title);
        }

        /// <summary>
        /// Create a page representation
        /// </summary>
        /// <returns>Page name containing namespace prefix and the title</returns>
        public override string ToString()
        {
            if (Namespace == Namespace.Main) return Title;
            else return String.Format(CultureInfo.CurrentCulture, "{0}:{1}", Namespace, Title);
        }
    }

    /// <summary>
    /// A category page
    /// </summary>
    public class Category : Page
    {
        /// <summary>
        /// Set of all subcategories of this category
        /// </summary>
        public HashSet<Category> Subcategories = new HashSet<Category>();

        /// <summary>
        /// Set of pages (non-category) inside this category
        /// </summary>
        public HashSet<Page> Articles = new HashSet<Page>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="title">Category title (excluding the namespace prefix)</param>
        /// <param name="id">Page identifier in database</param>
        public Category(string title, int id)
            : base(Namespace.Category, title, id, false)
        {
        }
    }

    /// <summary>
    /// An event in log (Special:Log)
    /// </summary>
    public class LoggedEvent
    {
        /// <summary>
        /// Type of the event
        /// </summary>
        public string Type;

        /// <summary>
        /// Action that generated the event
        /// </summary>
        public string Action;

        /// <summary>
        /// Timestamp of the event
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        /// Id of user that generated the event
        /// </summary>
        public int UserId;

        /// <summary>
        /// Namespace of the page relevant for the event
        /// </summary>
        public Namespace Namespace;

        /// <summary>
        /// Title of the page relevant for the event (without namespace prefix)
        /// </summary>
        public string Title;

        /// <summary>
        /// Comment for the event (added by the user)
        /// </summary>
        public string Comment;

        /// <summary>
        /// Other parameters for the event record
        /// </summary>
        public string Parameters;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type of the event</param>
        /// <param name="action">Action that generated the event</param>
        /// <param name="timestamp">Timestamp of the event</param>
        /// <param name="userId">Id of user that generated the event</param>
        /// <param name="ns">Namespace of the page relevant for the event</param>
        /// <param name="title">Title of the page relevant for the event (without namespace prefix)</param>
        /// <param name="comment">Comment for the event (added by the user)</param>
        /// <param name="parameters">Other parameters for the event record</param>
        public LoggedEvent(string type, string action, DateTime timestamp, int userId, Namespace ns, string title, string comment, string parameters)
        {
            Type = type;
            Action = action;
            Timestamp = timestamp;
            UserId = userId;
            Namespace = ns;
            Title = title;
            Comment = comment;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// A MediaWiki user
    /// </summary>
    public abstract class User : IComparable, IComparable<User>
    {
        /// <summary>
        /// Tests whether this instance describes the same user as another instance of the same type
        /// </summary>
        /// <param name="obj">Another instance of the same type</param>
        /// <returns><c>true</c> if this object is equal to <paramref name="obj"/>, <c>false</c> otherwise</returns>
        /// <remarks>Subclasses are expected to override this method with their own implementation</remarks>
        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        /// <summary>
        /// Compare this user to another user
        /// </summary>
        /// <param name="obj">Another instance of this type</param>
        /// <returns>
        /// +1 if this instance should sort after <paramref name="obj"/>,
        /// 0 if both instances are equal,
        /// -1 if this instance should sort before <paramref name="obj"/>
        /// </returns>
        /// <remarks>
        /// This generic version compares the string representations.
        /// Subclasses might want to provide better implementation.
        /// </remarks>
        public int CompareTo(object obj)
        {
            if (ReferenceEquals(obj, null)) return +1;
            User other = obj as User;
            if (other == null) throw new ArgumentException(LibraryResources.ArgumentMustBeUser, "obj");
            return CompareTo(other);
        }

        /// <summary>
        /// Get a hash code of this instance
        /// </summary>
        /// <returns>Hash code of the string representation of this user</returns>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Compare this user to another user
        /// </summary>
        /// <param name="other">Another user</param>
        /// <returns>
        /// +1 if this instance should sort after <paramref name="other"/>,
        /// 0 if both instances are equal,
        /// -1 if this instance should sort before <paramref name="other"/>
        /// </returns>
        /// <remarks>
        /// This generic version compares the string representations.
        /// Subclasses might want to provide better implementation.
        /// </remarks>
        public virtual int CompareTo(User other)
        {
            if (ReferenceEquals(other, null)) return +1;
            return ToString().CompareTo(other.ToString());
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <param name="user1">A user</param>
        /// <param name="user2">Another user</param>
        /// <returns><c>true</c> if both instances refer to the same user, <c>false</c> otherwise</returns>
        public static bool operator == (User user1, User user2)
        {
            if (ReferenceEquals(user1, null)) return ReferenceEquals(user2, null);
            return user1.Equals(user2);
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <param name="user1">A user</param>
        /// <param name="user2">Another user</param>
        /// <returns><c>false</c> if both instances refer to the same user, <c>true</c> otherwise</returns>
        public static bool operator !=(User user1, User user2)
        {
            return !(user1 == user2);
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <seealso cref="CompareTo(User)"/>
        public static bool operator <(User user1, User user2)
        {
            if (ReferenceEquals(user1, null)) return !ReferenceEquals(user2, null);
            return user1.CompareTo(user2) < 0;
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <seealso cref="CompareTo(User)"/>
        public static bool operator >(User user1, User user2)
        {
            if (ReferenceEquals(user1, null)) return false;
            return user1.CompareTo(user2) > 0;
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <seealso cref="CompareTo(User)"/>
        public static bool operator <=(User user1, User user2)
        {
            return !(user1 > user2);
        }

        /// <summary>
        /// Compares two user instances
        /// </summary>
        /// <seealso cref="CompareTo(User)"/>
        public static bool operator >=(User user1, User user2)
        {
            return !(user1 < user2);
        }
    }

    /// <summary>
    /// An anonymous (unregistered, or registered, but not logged-in) user, identified only by their IP address
    /// </summary>
    public class AnonymousUser : User
    {
        /// <summary>
        /// IP address of this user
        /// </summary>
        public string Ip;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ip">IP address of this user</param>
        public AnonymousUser(string ip)
        {
            Ip = ip;
        }

        /// <summary>
        /// Creates a user-readable representation of this user
        /// </summary>
        /// <returns>IP address of the user</returns>
        public override string ToString()
        {
            return Ip;
        }

        /// <summary>
        /// Checks whether this instance describes the same user (the same IP address) as another instance of this class
        /// </summary>
        /// <param name="obj">Another instance of this class</param>
        /// <returns><c>true</c> if <paramref name="obj"/> is an instance of this class describing the same user, <c>false</c> otherwise</returns>
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            AnonymousUser other = obj as AnonymousUser;
            if (other == null) return false;
            return Ip == other.Ip;
        }

        /// <summary>
        /// Compute hash code for this instance
        /// </summary>
        /// <returns>Hash of the IP address string representation</returns>
        public override int GetHashCode()
        {
            return Ip.GetHashCode();
        }

        /// <summary>
        /// Compares this instance to another user
        /// </summary>
        /// <param name="other">Another user</param>
        /// <remarks>Anonymous users sort smaller than registered users, in both classes, they are sorted alphabetically</remarks>
        /// <seealso cref="IComparable{T}.CompareTo"/>
        public override int CompareTo(User other)
        {
            AnonymousUser otherAnon = other as AnonymousUser;
            if (otherAnon != null) return Ip.CompareTo(otherAnon.Ip);
            RegisteredUser otherRegistered = other as RegisteredUser;
            if (otherRegistered != null) return -1;
            return base.CompareTo(other);
        }
    }

    /// <summary>
    /// A registered and logged-in user
    /// </summary>
    public class RegisteredUser : User
    {
        /// <summary>
        /// Name of this user
        /// </summary>
        public string UserName;

        /// <summary>
        /// Database user ID
        /// </summary>
        public int Id;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="userName">Name of this user</param>
        /// <param name="id">Database user ID</param>
        public RegisteredUser(string userName, int id)
        {
            UserName = userName;
            Id = id;
        }

        /// <summary>
        /// Creates a user-readable representation of this user
        /// </summary>
        /// <returns>Name of the user</returns>
        public override string ToString()
        {
            return UserName;
        }

        /// <summary>
        /// Tests whether this object represents the same user as another instance of the same class
        /// </summary>
        /// <param name="obj">Other instance of the same class</param>
        /// <returns><c>true</c> if this instance is equal to <paramref name="obj"/>, <c>false</c> otherwise</returns>
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            RegisteredUser other = obj as RegisteredUser;
            if (other == null) return false;
            return Id == other.Id;
        }

        /// <summary>
        /// Compute hash code for this instance
        /// </summary>
        /// <returns>Unique identifier of the user</returns>
        public override int GetHashCode()
        {
            return Id;
        }

        /// <summary>
        /// Compares this instance to another user
        /// </summary>
        /// <param name="other">Another user</param>
        /// <remarks>Anonymous users sort smaller than registered users, in both classes, they are sorted alphabetically</remarks>
        /// <seealso cref="IComparable{T}.CompareTo"/>
        public override int CompareTo(User other)
        {
            RegisteredUser otherRegistered = other as RegisteredUser;
            if (otherRegistered != null)
            {
                int result = Id.CompareTo(otherRegistered.Id);
                if (result != 0) return result;
                return UserName.CompareTo(otherRegistered.UserName);
            }
            AnonymousUser otherAnon = other as AnonymousUser;
            if (otherAnon != null) return +1;
            return base.CompareTo(other);
        }
    }

    /// <summary>
    /// A single revision of a page
    /// </summary>
    public class Revision
    {
        /// <summary>
        /// The page this revision is in
        /// </summary>
        public Page Page;

        /// <summary>
        /// Database revision identifier
        /// </summary>
        public int Id;

        /// <summary>
        /// Parent revision identifier
        /// </summary>
        public int ParentId;

        /// <summary>
        /// Date/time of this revision
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        /// The revision has been marked as a minor edit
        /// </summary>
        public bool Minor;

        /// <summary>
        /// The user that saved the revision
        /// </summary>
        public User Contributor;

        /// <summary>
        /// Edit summary for the revision
        /// </summary>
        public string Comment;

        /// <summary>
        /// Content model for the revision
        /// </summary>
        public string Model;

        /// <summary>
        /// Content format for the revision
        /// </summary>
        public string Format;

        /// <summary>
        /// Text of the page in this revision
        /// </summary>
        public string Text;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="page">The page this revision is in</param>
        /// <param name="id">Database revision identifier</param>
        /// <param name="parentId">Parent revision identifier</param>
        /// <param name="timestamp">Date/time of this revision</param>
        /// <param name="minor">The revision has been marked as a minor edit</param>
        /// <param name="contributor">The user that saved the revision</param>
        /// <param name="comment">Edit summary for the revision</param>
        /// <param name="model">Content model for the revision</param>
        /// <param name="format">Content format for the revision</param>
        /// <param name="text">Text of the page in this revision</param>
        public Revision(Page page, int id, int parentId, DateTime timestamp, bool minor, User contributor, string comment, string model, string format, string text)
        {
            this.Page = page;
            this.Id = id;
            this.ParentId = parentId;
            this.Timestamp = timestamp;
            this.Minor = minor;
            this.Contributor = contributor;
            this.Comment = comment ?? "";
            this.Model = model;
            this.Format = format;
            this.Text = text;
        }
    }

#if NUNIT
    /// <summary>
    /// Unit tests for <see cref="MWLib"/>
    /// </summary>
    [TestFixture]
    public class MWLib_Tests
    {
        /// <summary>
        /// Tests for <see cref="Page.ParseNamespace"/>
        /// </summary>
        public void TestParseNamespace()
        {
            var nsList = new Dictionary<int, string>();
            Assert.AreEqual(Namespace.Main, Page.ParseNamespace("Main", nsList));
            Assert.AreEqual(Namespace.Main, Page.ParseNamespace("2001", nsList));
        }
    }
#endif
}
