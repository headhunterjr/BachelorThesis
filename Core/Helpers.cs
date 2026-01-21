using MathNet.Numerics.LinearAlgebra;

namespace Core
{
    public class Helpers
    {
        public static double[,] MultiplyMatrices(double[,] A, double[,] B)
        {
            int rowsA = A.GetLength(0);
            int colsA = A.GetLength(1);
            int rowsB = B.GetLength(0);
            int colsB = B.GetLength(1);

            if (colsA != rowsB)
            {
                Console.WriteLine("Matrices are not compatible for multiplication.");
                throw new InvalidOperationException();
            }

            double[,] result = new double[rowsA, colsB];

            for (int i = 0; i < rowsA; ++i)
            {
                for (int j = 0; j < colsB; ++j)
                {
                    for (int k = 0; k < colsA; ++k)
                    {
                        result[i, j] += A[i, k] * B[k, j];
                    }
                }
            }
            return result;
        }
        public static double[,] ToColumnVector(double[] array)
        {
            if (array == null)
            {
                Console.WriteLine("Array is null.");
                throw new ArgumentNullException(nameof(array));
            }

            int n = array.Length;
            double[,] column = new double[n, 1];

            for (int i = 0; i < n; ++i)
            {
                column[i, 0] = array[i];
            }

            return column;
        }
        public static double[,] AddMatrices(double[,] A, double[,] B)
        {
            if (A == null || B == null)
            {
                Console.WriteLine("One of the matrices is null.");
                throw new ArgumentNullException(A == null ? nameof(A) : nameof(B));
            }

            int rowsA = A.GetLength(0);
            int colsA = A.GetLength(1);
            int rowsB = B.GetLength(0);
            int colsB = B.GetLength(1);

            if (rowsA != rowsB || colsA != colsB)
            {
                Console.WriteLine("Matrices must have the same dimensions for addition.");
                throw new InvalidOperationException("BRUH");
            }

            double[,] result = new double[rowsA, colsA];

            for (int i = 0; i < rowsA; ++i)
            {
                for (int j = 0; j < colsA; ++j)
                {
                    result[i, j] = A[i, j] + B[i, j];
                }
            }

            return result;
        }
        public static double[,] Identity(int n)
        {
            double[,] result = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                result[i, i] = 1.0;
            }
            return result;
        }
        public static void PlaceMatrix(double[,] target, double[,] source, int startRow, int startCol)
        {
            int sourceRows = source.GetLength(0);
            int sourceCols = source.GetLength(1);

            for (int i = 0; i < sourceRows; ++i)
            {
                for (int j = 0; j < sourceCols; ++j)
                {
                    target[startRow + i, startCol + j] = source[i, j];
                }
            }
        }
        public static double[,] MakeBlockDiagonal(double[,] block, int count)
        {
            int rows = block.GetLength(0);
            int cols = block.GetLength(1);

            double[,] result = new double[rows * count, cols * count];

            for (int i = 0; i < count; ++i)
            {
                PlaceMatrix(result, block, startRow: i * rows, startCol: i * cols);
            }

            return result;
        }
        public static double[,] Transpose(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] result = new double[cols, rows];

            for (int i = 0; i < rows; ++i)
            {
                for (int j = 0; j < cols; ++j)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            return result;
        }
        public static double[,] Inverse(double[,] matrix)
        {
            var M = Matrix<double>.Build.DenseOfArray(matrix);

            var M_inv = M.Inverse();

            return M_inv.ToArray();
        }
        public static double VectorQuadForm(double[,] vectorCol, double[,] matrix)
        {
            // 1. Transpose the column vector to get a row vector (x^T)
            double[,] vectorRow = Transpose(vectorCol);

            // 2. Multiply Row * Matrix (x^T * Q)
            double[,] temp = MultiplyMatrices(vectorRow, matrix);

            // 3. Multiply Result * Column ( (x^T * Q) * x )
            double[,] scalarMatrix = MultiplyMatrices(temp, vectorCol);

            // The result is a 1x1 matrix, just return the single value
            return scalarMatrix[0, 0];
        }
    }
}
