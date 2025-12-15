using System;

namespace NetShaper.Domain.Test
{
    // VIOLACIÓN R2.06: Comentario TODO encontrado.
    // TODO: Resolver este problema.

    public class ViolationTests
    {
        // VIOLACIÓN R7.01: El campo privado debe seguir el formato _camelCase.
        private int BadlyNamedField;

        public ViolationTests()
        {
            // VIOLACIÓN R1.03: El constructor tiene demasiados parámetros (5). El límite es 4.
            this.BadlyNamedField = 0;
        }

        // VIOLACIÓN R7.02: El miembro público debe usar PascalCase.
        public void badMethodName()
        {
            try
            {
                // VIOLACIÓN R12.03: El número 123 es un número mágico.
                var magic = 123;
                if (System.DateTime.Now.Day == magic)
                    throw new Exception("Test");
            }
            // VIOLACIÓN R6.03: Bloque catch vacío.
            catch
            {
            }
        }
    }
}
