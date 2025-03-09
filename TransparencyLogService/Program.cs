using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MerkleTransparencyLog
{
    // Represents a node in the Merkle Tree
    public class MerkleNode
    {
        public string Hash { get; set; }
        public MerkleNode Left { get; set; }
        public MerkleNode Right { get; set; }

        public MerkleNode(string hash)
        {
            Hash = hash;
        }
    }

    // Transparency Log Service using Merkle Tree
    public class TransparencyLogService
    {
        private List<string> _logEntries = new List<string>();
        private MerkleNode _root;

        // Add a new entry to the log
        public void AddEntry(string data)
        {
            _logEntries.Add(ComputeHash(data));
            BuildMerkleTree();
            Console.WriteLine($"Entry added: {data}");
        }

        // Build the Merkle Tree from log entries
        private void BuildMerkleTree()
        {
            var nodes = new List<MerkleNode>();
            foreach (var entry in _logEntries)
            {
                nodes.Add(new MerkleNode(entry));
            }

            while (nodes.Count > 1)
            {
                var parentNodes = new List<MerkleNode>();
                for (int i = 0; i < nodes.Count; i += 2)
                {
                    var left = nodes[i];
                    var right = (i + 1 < nodes.Count) ? nodes[i + 1] : null;

                    string combinedHash = right == null ? left.Hash : ComputeHash(left.Hash + right.Hash);
                    var parentNode = new MerkleNode(combinedHash) { Left = left, Right = right };
                    parentNodes.Add(parentNode);
                }
                nodes = parentNodes;
            }

            _root = nodes[0]; // Root of the tree
        }

        // Generate an inclusion proof for a specific entry
        public List<string> GenerateInclusionProof(string data)
        {
            string targetHash = ComputeHash(data);
            var proof = new List<string>();
            FindInclusionProof(_root, targetHash, proof);
            return proof;
        }

        private bool FindInclusionProof(MerkleNode node, string targetHash, List<string> proof)
        {
            if (node == null) return false;

            if (node.Hash == targetHash)
                return true;

            if (FindInclusionProof(node.Left, targetHash, proof))
            {
                if (node.Right != null) proof.Add(node.Right.Hash);
                return true;
            }

            if (FindInclusionProof(node.Right, targetHash, proof))
            {
                if (node.Left != null) proof.Add(node.Left.Hash);
                return true;
            }

            return false;
        }

        // Verify an inclusion proof
        public bool VerifyInclusionProof(string data, List<string> proof, string rootHash)
        {
            string hash = ComputeHash(data);

            foreach (var siblingHash in proof)
            {
                hash = ComputeHash(hash + siblingHash);
            }

            return hash == rootHash;
        }

        // Get the current root hash of the Merkle Tree
        public string GetRootHash()
        {
            return _root?.Hash ?? string.Empty;
        }

        // Compute SHA-256 hash
        private string ComputeHash(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var logService = new TransparencyLogService();

            // Add entries to the transparency log
            logService.AddEntry("TrustedSoftware_v1");
            logService.AddEntry("TrustedSoftware_v2");

            Console.WriteLine($"Current Root Hash: {logService.GetRootHash()}");

            // Generate inclusion proof for an entry
            string dataToVerify = "TrustedSoftware_v1";
            var inclusionProof = logService.GenerateInclusionProof(dataToVerify);

            Console.WriteLine($"Inclusion Proof for '{dataToVerify}':");
            foreach (var hash in inclusionProof)
                Console.WriteLine(hash);

            // Verify inclusion proof
            bool isValid = logService.VerifyInclusionProof(dataToVerify, inclusionProof, logService.GetRootHash());

            Console.WriteLine($"Is Inclusion Proof Valid? {isValid}");
        }
    }
}
