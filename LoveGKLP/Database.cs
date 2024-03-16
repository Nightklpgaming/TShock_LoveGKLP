using MySql.Data.MySqlClient;
using System.Data;
using TShockAPI.DB;

namespace LoveGKLP
{
    public class Database
    {
        //using someones plugin code since this is easy...
        public class DBManager
        {
            private IDbConnection _db;

            public DBManager(IDbConnection db)
            {
                _db = db;

                var sqlCreator = new SqlTableCreator(db, new SqliteQueryCreator());

                sqlCreator.EnsureTableStructure(new SqlTable("Partners",
                    new SqlColumn("player1", MySqlDbType.String) { Unique = true },
                    new SqlColumn("player2", MySqlDbType.String) { Unique = true },
                    new SqlColumn("type", MySqlDbType.String)));

                sqlCreator.EnsureTableStructure(new SqlTable("Players",
                    new SqlColumn("Name", MySqlDbType.String) { Primary = true, Unique = true },
                    new SqlColumn("Gender", MySqlDbType.String),
                    new SqlColumn("Age", MySqlDbType.Int32)));//age will be unused
            }

            /// <exception cref="NullReferenceException"></exception>

            //get data

            public Partner GetPartnerByName(string name)
            {
                try
                {
                    Partner getreader = this.GetPartnerByPlayer1(name);
                    return getreader;
                } catch (NullReferenceException)
                {
                    using var reader = _db.QueryReader("SELECT * FROM Partners WHERE player2 = @0", name);
                    while (reader.Read())
                    {
                        return new Partner(
                            reader.Get<string>("player1"),
                            reader.Get<string>("player2"),
                            reader.Get<string>("Type")
                        );
                    }
                }
                throw new NullReferenceException();
            }

            public Partner GetPartnerByPlayer1(string name)
            {
                using var reader = _db.QueryReader("SELECT * FROM Partners WHERE player1 = @0", name);
                while (reader.Read())
                {
                    return new Partner(
                        reader.Get<string>("player1"),
                        reader.Get<string>("player2"),
                        reader.Get<string>("Type")
                    );
                }
                throw new NullReferenceException();
            }

            public usergklp GetPlayerByName(string name)
            {
                using var reader = _db.QueryReader("SELECT * FROM Players WHERE Name = @0", name);
                while (reader.Read())
                {
                    return new usergklp(
                        reader.Get<string>("Name"),
                        reader.Get<string>("Gender"),
                        reader.Get<int>("Age")
                    );
                }
                throw new NullReferenceException();
            }
            //Add

            public bool NewPartner(string player1, string player2)
            {
                return _db.Query("INSERT INTO Partners (player1, player2, Type) VALUES (@0, @1, @2)", player1, player2, "couples") != 0;
            }
            
            public bool NewPlayer(string Name, string Gender)
            {
                return _db.Query("INSERT INTO Players (Name, Gender, Age) VALUES (@0, @1, @2)", Name, Gender, 0) != 0;
            }

            //delete
            public bool DeletePartner(string name)
            {
                try
                {
                    bool check = _db.Query("DELETE FROM Partners WHERE Player1 = @0", name) != 0;
                    if (!check)
                    {
                        return _db.Query("DELETE FROM Partners WHERE Player2 = @0", name) != 0;
                    }
                    else
                    {
                        return check;
                    }
                }
                catch (NullReferenceException)
                {
                    return _db.Query("DELETE FROM Partners WHERE Player2 = @0", name) != 0;
                }

                throw new NullReferenceException();
            }


            //modify

            public bool SetPlayerGender(string name, string gender)
            {
                return _db.Query("UPDATE Players SET Gender = @0 WHERE Name = @1", gender, name) != 0;
                throw new NullReferenceException();
            }

            public bool SetPartnerType(string name, string type)
            {
                try
                {
                    bool check = _db.Query("UPDATE Partners SET Type = @0 WHERE Player1 = @1", type, name) != 0;
                    if (!check)
                    {
                        return _db.Query("UPDATE Partners SET Type = @0 WHERE Player2 = @1", type, name) != 0;
                    } else
                    {
                        return check;
                    }
                }
                catch (NullReferenceException)
                {
                    return _db.Query("UPDATE Partners SET Type = @0 WHERE Player2 = @1", type, name) != 0;
                }
                
                throw new NullReferenceException();
            }
        }
    }
    public class Partner
    {
        public string Player1;
        public string Player2;
        public string Type;

        public Partner(string Player1 = null, string Player2 = null, string Type = null)
        {
            this.Player1 = Player1;
            this.Player2 = Player2;
            this.Type = Type;
        }
    }

    public class usergklp
    {
        public string Name;
        public string Gender;
        public int Age;//unused

        public usergklp(string Name, string Gender, int Age)
        {
            this.Name = Name;
            this.Gender = Gender;
            this.Age = Age;
        }
    }
}
