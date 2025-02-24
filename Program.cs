using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;

class Program
{
    static string connectionString = "Data Source=tareas.db;Version=3;";

    static void Main()
    {
        CrearBaseDeDatos();
        while (true)
        {
            Console.Clear();
            Console.WriteLine("RAMA | TaskTimer v0.0.1");
            Console.WriteLine();
            Console.WriteLine("Menú:");
            Console.WriteLine("1. Crear tarea");
            Console.WriteLine("2. Lista de tareas");
            Console.WriteLine("3. Eliminar tarea");
            Console.WriteLine("4. Salir");
            Console.Write("Seleccione una opción: ");
            string opcion = Console.ReadLine();

            switch (opcion)
            {
                case "1": CrearTarea(); break;
                case "2": SeleccionarTarea(); break;
                case "3": EliminarTarea(); break;
                case "4": return;
                default: Console.WriteLine("Opción no válida"); break;
            }
        }
    }

    static void CrearBaseDeDatos()
    {
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "CREATE TABLE IF NOT EXISTS tareas (id INTEGER PRIMARY KEY, nombre TEXT, tiempo INTEGER)";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    static void CrearTarea()
    {
        Console.Write("Nombre de la tarea: ");
        string nombre = Console.ReadLine();
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "INSERT INTO tareas (nombre, tiempo) VALUES (@nombre, 0)";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.ExecuteNonQuery();
            }
        }
        Console.WriteLine("Tarea creada. Presione una tecla para continuar...");
        Console.ReadKey();
    }

    static void SeleccionarTarea()
    {
        List<(int, string, int)> tareas = ObtenerTareas();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas disponibles.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo: {tareas[i].Item3} seg)");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            IniciarContador(tareas[seleccion - 1].Item1, tareas[seleccion - 1].Item2, tareas[seleccion - 1].Item3);
        }
        else
        {
            Console.WriteLine("Selección inválida.");
            Console.ReadKey();
        }
    }

    static List<(int, string, int)> ObtenerTareas()
    {
        var tareas = new List<(int, string, int)>();
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "SELECT * FROM tareas";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tareas.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
                }
            }
        }
        return tareas;
    }

    static void IniciarContador(int id, string nombre, int tiempoInicial)
    {
        Console.WriteLine($"Iniciando tarea: {nombre}");
        Stopwatch sw = new Stopwatch();
        sw.Start();
        Console.WriteLine("Presione cualquier tecla para detener...");
        Console.ReadKey();
        sw.Stop();

        int tiempoTotal = tiempoInicial + (int)sw.Elapsed.TotalSeconds;
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "UPDATE tareas SET tiempo = @tiempo WHERE id = @id";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@tiempo", tiempoTotal);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
        Console.WriteLine($"Tiempo registrado: {tiempoTotal} segundos. Presione una tecla para continuar...");
        Console.ReadKey();
    }

    static void EliminarTarea()
    {
        List<(int, string, int)> tareas = ObtenerTareas();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas para eliminar.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea para eliminar:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo: {tareas[i].Item3} seg)");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string sql = "DELETE FROM tareas WHERE id = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", tareas[seleccion - 1].Item1);
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("Tarea eliminada.");
        }
        else
        {
            Console.WriteLine("Selección inválida.");
        }
        Console.ReadKey();
    }
}
